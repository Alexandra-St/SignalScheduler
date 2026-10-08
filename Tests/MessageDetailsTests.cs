using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SignalScheduler.Models;
using SignalScheduler.ViewModels;
using SignalScheduler.Views;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class MessageDetailsTests
{
    [AvaloniaTheory]
    [InlineData(MessageStatus.Pending)]
    [InlineData(MessageStatus.Sending)]
    [InlineData(MessageStatus.Sent)]
    [InlineData(MessageStatus.Cancelled)]
    [InlineData(MessageStatus.Missed)]
    [InlineData(MessageStatus.Blocked)]
    [InlineData(MessageStatus.Unknown)]
    [InlineData(MessageStatus.UnknownOrFailed)]
    public void DetailsShowFullContentAllAttachmentsAndOnlyValidActions(MessageStatus status)
    {
        var owner = new MainWindowViewModel();
        const string text = "First line\nSecond line\n\nLast line with the complete message.";
        var photos = Enumerable.Range(1, 5).Select(i => new ImageAttachment($"synthetic-{i}.png", new byte[] { 1, 2, 3 })).ToList();
        using var model = new MessageViewModel(new ScheduledMessage(Guid.NewGuid(), "Synthetic_User.27", text,
            DateTimeOffset.Now.AddHours(2), status, "synthetic-account", "synthetic-cli", photos), owner);
        var window = new MessageDetailsWindow(owner, model);
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Equal(text, window.FindControl<SelectableTextBlock>("FullMessageText")!.Text);
            Assert.Equal(5, window.FindControl<ItemsControl>("DetailsAttachments")!.ItemCount);
            Assert.Equal(model.HasStatusHint, window.FindControl<TextBlock>("DetailsWarning")!.IsEffectivelyVisible);
            if (status is MessageStatus.Unknown or MessageStatus.UnknownOrFailed)
                Assert.Contains("avoid duplicate", window.FindControl<TextBlock>("DetailsWarning")!.Text);
            var buttons = window.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.Equal(status == MessageStatus.Pending, buttons.Single(b => Equals(b.Content, "Cancel message")).IsVisible);
            Assert.Equal(status == MessageStatus.Pending, buttons.Single(b => Equals(b.Content, "Edit text…")).IsVisible);
            Assert.Equal(status is not (MessageStatus.Sending or MessageStatus.Missed), buttons.Single(b => Equals(b.Content, "Use as new message")).IsVisible);
            Assert.Equal(model.CanDelete, buttons.Single(b => Equals(b.Content, "Delete from history") && b.Parent is WrapPanel).IsVisible);
            Assert.False(window.FindControl<Border>("DeleteConfirmation")!.IsVisible);
            if (model.CanDelete)
            {
                buttons.Single(b => Equals(b.Content, "Delete from history") && b.Parent is WrapPanel)
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(window.FindControl<Border>("DeleteConfirmation")!.IsVisible);
                Assert.False(window.FindControl<WrapPanel>("DetailsActions")!.IsVisible);
                Assert.Equal(new[] { "Cancel", "Delete" }, buttons.Where(b => b.IsEffectivelyVisible && b.Content is string).Select(b => (string)b.Content!).OrderBy(t => t).ToArray());
                buttons.Single(b => Equals(b.Content, "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(window.FindControl<Border>("DeleteConfirmation")!.IsVisible);
            }
            if (status == MessageStatus.Pending && Environment.GetEnvironmentVariable("SIGNALSCHEDULER_LAYOUT_PREVIEW") is { } directory)
            {
                Directory.CreateDirectory(directory);
                using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
                frame.Save(Path.Combine(directory, "message-details.png"));
            }
        }
        finally { window.Close(); owner.TryClose(); }
    }

    [AvaloniaFact]
    public void OpenDetailsRefreshWhenPendingStartsSendingAndStaleActionsAreRejected()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromHours(1));
        var owner = new MainWindowViewModel();
        // Inject only an isolated encrypted fixture. Never call OpenAsync/Keychain/Signal.
        typeof(MainWindowViewModel).GetField("store", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(owner, queue.Store);
        var refresh = typeof(MainWindowViewModel).GetMethod("UpdateMessages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        refresh.Invoke(owner, null);
        var window = new MessageDetailsWindow(owner, Assert.Single(owner.Messages));
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            queue.Store.ChangeStatus(message.Id, MessageStatus.Sending);
            refresh.Invoke(owner, null); Dispatcher.UIThread.RunJobs();
            var current = Assert.IsType<MessageViewModel>(window.DataContext);
            Assert.Equal(MessageStatus.Sending, current.Message.State);
            Assert.False(current.CanCancel);
            Assert.False(current.CanDelete);
            Assert.False(current.CanEdit);
            Assert.False(current.CanReuse);
            owner.Cancel(message);
            owner.Delete(message);
            Assert.Equal(MessageStatus.Sending, Assert.Single(queue.Store.Items).State);
        }
        finally { window.Close(); owner.TryClose(); }
    }

    [AvaloniaFact]
    public void ReuseFromDetailsCopiesFullTextWithoutScheduling()
    {
        var owner = new MainWindowViewModel();
        using var model = new MessageViewModel(new ScheduledMessage(Guid.NewGuid(), "Synthetic_User.27", "Full\ntext",
            DateTimeOffset.Now.AddHours(2), MessageStatus.Sent, "synthetic-account", "synthetic-cli"), owner);
        var window = new MessageDetailsWindow(owner, model);
        window.Show(); Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Use as new message"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("Full\ntext", owner.Body);
        Assert.Equal("Synthetic_User.27", owner.Recipient);
        Assert.Empty(owner.Messages);
        owner.TryClose();
    }
}
