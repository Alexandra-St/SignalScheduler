using Avalonia.Controls;
using System.Reflection;
using Avalonia.Headless.XUnit;
using SignalScheduler.Models;
using SignalScheduler.Services;
using SignalScheduler.ViewModels;
using Xunit;
namespace SignalScheduler.Tests;
public sealed class RescheduleTests
{
    private static void Set(MainWindowViewModel owner, string name, object value)
        => typeof(MainWindowViewModel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(owner, value);
    private static void Call(MainWindowViewModel owner, string name)
        => typeof(MainWindowViewModel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner, null);
    [AvaloniaFact]
    public void HistoryComboBoxKeepsEverySelectionWithAndWithoutStartupNotice()
    {
        using var queue = new TestQueue();
        var fresh = queue.Add(TimeSpan.FromHours(-1), MessageStatus.Missed);
        queue.Add(TimeSpan.FromHours(-2), MessageStatus.Missed);
        queue.Add(TimeSpan.FromHours(-3), MessageStatus.Sent);
        queue.Add(TimeSpan.FromHours(-4), MessageStatus.Cancelled);
        var owner = new MainWindowViewModel(); Set(owner, "store", queue.Store);
        var combo = new ComboBox { DataContext = owner };
        combo.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("HistoryFilters"));
        combo.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty,
            new Avalonia.Data.Binding("HistoryFilter") { Mode = Avalonia.Data.BindingMode.TwoWay });
        var host = new Window { Content = combo }; host.Show();
        try
        {
            foreach (var hasNotice in new[] { false, true })
            {
                Set(owner, "startupMissedIds", hasNotice ? new HashSet<Guid> { fresh.Id } : new HashSet<Guid>());
                Call(owner, "UpdateMessages");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (hasNotice) owner.ReviewMissedCommand.Execute(null);
                foreach (var filter in owner.HistoryFilters.ToArray())
                {
                    combo.SelectedItem = filter;
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Assert.Equal(filter, owner.HistoryFilter);
                    Assert.Equal(filter, combo.SelectedItem);
                    Assert.All(owner.HistoryMessages, item => Assert.True(filter switch
                    {
                        "Sent" => item.Message.State == MessageStatus.Sent,
                        "Cancelled" => item.Message.State == MessageStatus.Cancelled,
                        "Missed" => item.Message.State == MessageStatus.Missed,
                        "Missed (1)" => item.Message.Id == fresh.Id,
                        "All statuses" => true,
                        _ => false
                    }));
                }
            }
        }
        finally { host.Close(); owner.TryClose(); }
    }

    [AvaloniaFact]
    public void RescheduleDialogCancelDoesNotCreateMessageAndShowsFilledContent()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromHours(-1), MessageStatus.Missed);
        var owner = new MainWindowViewModel(); Set(owner, "store", queue.Store);
        var dialog = new SignalScheduler.Views.RescheduleWindow(owner, message);
        dialog.Show(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(message, Assert.IsType<MessageViewModel>(dialog.DataContext).Message);
        Assert.False(string.IsNullOrWhiteSpace(dialog.FindControl<Avalonia.Controls.TextBox>("NewDate")!.Text));
        Assert.False(string.IsNullOrWhiteSpace(dialog.FindControl<Avalonia.Controls.TextBox>("NewTime")!.Text));
        dialog.Close(false);
        Assert.Equal(message, Assert.Single(queue.Store.Items));
        owner.TryClose();
    }

    [AvaloniaFact]
    public async Task ConfirmCreatesNewPendingAndPreservesMissedContentAndAccount()
    {
        using var queue = new TestQueue();
        var original = queue.Add(TimeSpan.FromHours(-1), MessageStatus.Missed);
        queue.Store.Remove(original.Id);
        original = original with { Attachments = new() { new("synthetic.png", new byte[] { 1, 2, 3 }) } };
        queue.Store.Add(original);
        File.WriteAllText(queue.Executable, "#!/bin/sh\nif [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.9'; else printf '[{\"number\":\"account-placeholder\"}]'; fi\n");
        File.SetUnixFileMode(queue.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var owner = new MainWindowViewModel();
        Set(owner, "store", queue.Store); Set(owner, "canSchedule", true);
        await owner.Signal.ConfigureExecutableAsync(queue.Executable, persist: false);
        // The missed original belongs to the current startup notification.
        Set(owner, "startupMissedIds", new HashSet<Guid> { original.Id });
        Call(owner, "UpdateMessages");
        Assert.True(owner.HasStartupMissed);
        var future = DateTime.Now.AddHours(2);
        Assert.True(owner.TryReschedule(original.Id, future.ToString("dd.MM.yyyy"), future.ToString("HH:mm"), out var error), error);
        Assert.Equal(2, queue.Store.Items.Count);
        Assert.False(owner.HasStartupMissed);
        Call(owner, "UpdateMessages");
        Assert.False(owner.HasStartupMissed);
        Assert.Equal(original, queue.Store.Items.Single(item => item.Id == original.Id));
        var pending = queue.Store.Items.Single(item => item.Id != original.Id);
        Assert.Equal(MessageStatus.Pending, pending.State);
        Assert.Equal(original.Text, pending.Text);
        Assert.Equal(original.Recipient, pending.Recipient);
        Assert.Equal(original.Account, pending.Account);
        Assert.Equal(original.Attachments, pending.Attachments);
        Assert.Equal(0, owner.SelectedMessagesTab);
        Assert.False(owner.TryReschedule(original.Id, "31.02.2027", "12:00", out _));
        Assert.False(owner.TryReschedule(original.Id, "01.01.2020", "12:00", out _));
        Assert.False(owner.TryReschedule(pending.Id, future.ToString("dd.MM.yyyy"), "12:00", out _));
        Assert.Equal(2, queue.Store.Items.Count);
        owner.TryClose();
    }
    [AvaloniaFact]
    public async Task StartupNoticeCountsOnlyNewlyMissedAndReviewFiltersHistory()
    {
        using var queue = new TestQueue();
        queue.Add(TimeSpan.FromHours(-2), MessageStatus.Missed);
        queue.Add(TimeSpan.FromHours(-3), MessageStatus.Missed);
        queue.Add(TimeSpan.FromHours(-1)); queue.Add(TimeSpan.FromMinutes(-10));
        queue.Add(TimeSpan.FromHours(1));
        var owner = new MainWindowViewModel(); Set(owner, "store", queue.Store);
        Call(owner, "CaptureStartupOverdue");
        var sender = new FakeSignalSender();
        var dispatcher = new MessageDispatcher(queue.Store, sender);
        await dispatcher.DispatchDueAsync(); await dispatcher.DispatchDueAsync();
        Call(owner, "UpdateMessages"); Call(owner, "UpdateMessages");
        Assert.Empty(sender.Attempts);
        Assert.True(owner.HasStartupMissed);
        Assert.StartsWith("2 scheduled messages", owner.StartupMissedNotice);
        Assert.Contains("inactive", owner.StartupMissedNotice);
        owner.ReviewMissedCommand.Execute(null);
        Assert.Equal(1, owner.SelectedMessagesTab);
        Assert.Equal("Missed (2)", owner.HistoryFilter);
        Assert.Equal(2, owner.HistoryMessages.Count);
        Assert.Equal("Show all missed (4)", owner.ShowAllMissedLabel);
        owner.ShowAllMissedCommand.Execute(null);
        Assert.Equal(4, owner.HistoryMessages.Count);
        Assert.False(owner.IsReviewingNewMissed);
        owner.ReviewMissedCommand.Execute(null);
        Assert.Equal(2, owner.HistoryMessages.Count);
        owner.Delete(queue.Store.Items.First(item => item.State == MessageStatus.Missed && item.Due > DateTimeOffset.Now.AddMinutes(-90)));
        Assert.StartsWith("1 scheduled message", owner.StartupMissedNotice);
        owner.DismissMissedCommand.Execute(null);
        Assert.False(owner.HasStartupMissed);
        Call(owner, "UpdateMessages");
        Assert.False(owner.HasStartupMissed);
        owner.TryClose();
    }
}
