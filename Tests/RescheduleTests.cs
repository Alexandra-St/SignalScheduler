using Avalonia.Controls;
using SignalScheduler.Presentation;
using Avalonia.Headless.XUnit;
using SignalScheduler.Models;
using SignalScheduler.Services;
using SignalScheduler.ViewModels;
using Xunit;
namespace SignalScheduler.Tests;
public sealed class RescheduleTests
{
    [AvaloniaFact]
    public async Task StartupWriteFailureStopsProcessingWithoutSendingOrClaimingSuccess()
    {
        using var queue = new TestQueue();
        queue.Add(TimeSpan.FromHours(-1));
        var sender = new FakeSignalSender();
        var owner = new MainWindowViewModel(queue.Store, sender);
        // Prevent the atomic save from creating its temporary file.
        Directory.CreateDirectory(Path.Combine(queue.DirectoryPath, "queue.enc.tmp"));
        try
        {
            await Assert.ThrowsAsync<IOException>(() => owner.ProcessStartupOverdueAsync());
            Assert.Empty(sender.Attempts);
            Assert.False(owner.HasStartupMissed);
            Assert.Contains("Queue write failed", owner.Status);
        }
        finally { owner.TryClose(); }
    }

    [AvaloniaFact]
    public async Task HistoryComboBoxKeepsEverySelectionWithAndWithoutStartupNotice()
    {
        foreach (var hasNotice in new[] { false, true })
        {
            using var queue = new TestQueue();
            var fresh = queue.Add(TimeSpan.FromHours(-1), hasNotice ? MessageStatus.Pending : MessageStatus.Missed);
            queue.Add(TimeSpan.FromHours(-2), MessageStatus.Missed);
            queue.Add(TimeSpan.FromHours(-3), MessageStatus.Sent);
            queue.Add(TimeSpan.FromHours(-4), MessageStatus.Cancelled);
            var owner = new MainWindowViewModel(queue.Store, new FakeSignalSender());
            await owner.ProcessStartupOverdueAsync();
            var combo = new ComboBox { DataContext = owner };
            combo.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("HistoryFilters"));
            combo.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty,
                new Avalonia.Data.Binding("HistoryFilter") { Mode = Avalonia.Data.BindingMode.TwoWay });
            var host = new Window { Content = combo }; host.Show();
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (hasNotice) owner.ReviewMissedCommand.Execute(null);
                foreach (var filter in owner.HistoryFilters.ToArray())
                {
                    combo.SelectedItem = filter;
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Assert.Equal(filter, owner.HistoryFilter);
                    Assert.Equal(filter, combo.SelectedItem);
                    Assert.All(owner.HistoryMessages, item => Assert.True(filter.Kind switch
                    {
                        HistoryFilterKind.Sent => item.Message.State == MessageStatus.Sent,
                        HistoryFilterKind.Cancelled => item.Message.State == MessageStatus.Cancelled,
                        HistoryFilterKind.Missed => item.Message.State == MessageStatus.Missed,
                        HistoryFilterKind.NewMissed => item.Message.Id == fresh.Id,
                        HistoryFilterKind.All => true,
                        _ => false
                    }));
                }
            }
            finally { host.Close(); owner.TryClose(); }
        }
    }

    [AvaloniaFact]
    public void RescheduleDialogCancelDoesNotCreateMessageAndShowsFilledContent()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromHours(-1), MessageStatus.Missed);
        var owner = new MainWindowViewModel(queue.Store, new FakeSignalSender());
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
        var original = queue.Add(TimeSpan.FromHours(-1));
        queue.Store.Remove(original.Id);
        original = original with { Attachments = new() { new("synthetic.png", new byte[] { 1, 2, 3 }) } };
        queue.Store.Add(original);
        File.WriteAllText(queue.Executable, "#!/bin/sh\nif [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.9'; else printf '[{\"number\":\"account-placeholder\"}]'; fi\n");
        File.SetUnixFileMode(queue.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var owner = new MainWindowViewModel(queue.Store, new FakeSignalSender());
        await owner.Signal.ConfigureExecutableAsync(queue.Executable, persist: false);
        await owner.ProcessStartupOverdueAsync();
        original = queue.Store.Items.Single();
        Assert.True(owner.HasStartupMissed);
        var future = DateTime.Now.AddHours(2);
        Assert.True(owner.TryReschedule(original.Id, future.ToString("dd.MM.yyyy"), future.ToString("HH:mm"), out var error), error);
        Assert.Equal(2, queue.Store.Items.Count);
        Assert.False(owner.HasStartupMissed);
        await owner.ProcessStartupOverdueAsync();
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
        var sender = new FakeSignalSender();
        var owner = new MainWindowViewModel(queue.Store, sender);
        await owner.ProcessStartupOverdueAsync();
        await owner.ProcessStartupOverdueAsync(); // Reprocessing must neither duplicate notices nor send.
        Assert.Empty(sender.Attempts);
        Assert.Same(owner.ReviewMissedCommand, owner.ReviewMissedCommand);
        Assert.Same(owner.DismissMissedCommand, owner.DismissMissedCommand);
        Assert.Same(owner.ShowAllMissedCommand, owner.ShowAllMissedCommand);
        Assert.True(owner.HasStartupMissed);
        Assert.StartsWith("2 scheduled messages", owner.StartupMissedNotice);
        Assert.Contains("inactive", owner.StartupMissedNotice);
        owner.ReviewMissedCommand.Execute(null);
        Assert.Equal(1, owner.SelectedMessagesTab);
        Assert.Equal(HistoryFilterKind.NewMissed, owner.HistoryFilter.Kind);
        Assert.Equal("Missed (2)", owner.HistoryFilter.Label);
        Assert.Equal(2, owner.HistoryMessages.Count);
        Assert.Equal("Show all missed (4)", owner.ShowAllMissedLabel);
        owner.ShowAllMissedCommand.Execute(null);
        Assert.Equal(4, owner.HistoryMessages.Count);
        Assert.False(owner.IsReviewingNewMissed);
        owner.ReviewMissedCommand.Execute(null);
        Assert.Equal(2, owner.HistoryMessages.Count);
        owner.Delete(queue.Store.Items.First(item => item.State == MessageStatus.Missed && item.Due > DateTimeOffset.Now.AddMinutes(-90)));
        Assert.StartsWith("1 scheduled message", owner.StartupMissedNotice);
        Assert.Equal("Missed (1)", owner.HistoryFilter.Label);
        Assert.Single(owner.HistoryMessages);
        Assert.Equal("Show all missed (3)", owner.ShowAllMissedLabel);
        owner.DismissMissedCommand.Execute(null);
        Assert.False(owner.HasStartupMissed);
        await owner.ProcessStartupOverdueAsync();
        Assert.False(owner.HasStartupMissed);
        owner.TryClose();
    }
}
