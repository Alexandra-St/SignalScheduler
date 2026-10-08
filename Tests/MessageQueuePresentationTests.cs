using Avalonia.Headless.XUnit;
using SignalScheduler.Models;
using SignalScheduler.Presentation;
using SignalScheduler.ViewModels;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class MessageQueuePresentationTests
{
    [AvaloniaFact]
    public void UpcomingGroupsLocalDatesAndSortsNearestFirstWithoutHistory()
    {
        var owner = new MainWindowViewModel();
        var today = DateTime.Today;
        MessageViewModel Make(DateTime due, MessageStatus status) => new(new ScheduledMessage(Guid.NewGuid(),
            "+12025550123", "Synthetic", new DateTimeOffset(due), status, "fixture", "fixture-cli"), owner);
        var messages = new[] { Make(today.AddDays(4), MessageStatus.Pending), Make(today.AddHours(15), MessageStatus.Pending),
            Make(today.AddDays(1).AddHours(9), MessageStatus.Sending), Make(today.AddHours(9), MessageStatus.Pending),
            Make(today.AddHours(8), MessageStatus.Sent) };
        var groups = MessageQueuePresentation.Upcoming(messages, today);
        Assert.Equal(new[] { "Today", "Tomorrow", "Later" }, groups.Select(group => group.Title));
        Assert.Equal(new[] { 9, 15 }, groups[0].Items.Select(message => message.Message.Due.LocalDateTime.Hour));
        Assert.Equal(4, groups.Sum(group => group.Items.Count));
        Assert.Equal("Today", MessageQueuePresentation.Upcoming(messages, today.AddDays(1))[0].Title);
        owner.TryClose();
    }

    [AvaloniaFact]
    public void HistoryContainsEveryTerminalStatusAndFilterCombinesUncertainResults()
    {
        var owner = new MainWindowViewModel();
        var messages = Enum.GetValues<MessageStatus>().Select((status, index) => new MessageViewModel(
            new ScheduledMessage(Guid.NewGuid(), "+12025550123", "Synthetic", DateTimeOffset.Now.AddHours(index),
                status, "fixture", "fixture-cli"), owner)).ToArray();
        var history = MessageQueuePresentation.History(messages, HistoryFilterKind.All);
        Assert.Equal(6, history.Count);
        Assert.Equal(history.OrderByDescending(message => message.Message.Due), history);
        Assert.DoesNotContain(history, message => message.Message.State is MessageStatus.Pending or MessageStatus.Sending);
        foreach (var filter in new[] { HistoryFilterKind.Sent, HistoryFilterKind.Cancelled, HistoryFilterKind.Missed, HistoryFilterKind.Blocked })
            Assert.Single(MessageQueuePresentation.History(messages, filter));
        var uncertain = MessageQueuePresentation.History(messages, HistoryFilterKind.Uncertain);
        Assert.Equal(2, uncertain.Count);
        Assert.All(uncertain, message => Assert.True(message.HasStatusHint));
        Assert.Empty(MessageQueuePresentation.History(Array.Empty<MessageViewModel>(), HistoryFilterKind.All));
        owner.TryClose();
    }

    [AvaloniaFact]
    public void CardsLimitPreviewsToThreeAndDoNotLoseAttachmentsOrFullRecipient()
    {
        var owner = new MainWindowViewModel();
        var attachments = Enumerable.Range(0, 5).Select(index => new ImageAttachment($"fixture-{index}.png", new byte[] { 1 })).ToList();
        using var message = new MessageViewModel(new ScheduledMessage(Guid.NewGuid(), "Test_User.27", "First line\nSecond line",
            DateTimeOffset.Now, MessageStatus.UnknownOrFailed, "fixture", "fixture-cli", attachments), owner);
        Assert.Equal(3, message.PreviewAttachments.Count);
        Assert.Equal(5, message.Attachments.Count);
        Assert.True(message.HasMoreAttachments);
        Assert.Equal("+2", message.MoreAttachments);
        Assert.Equal("Test_User.27", message.RecipientIdentifier);
        Assert.Equal("Unknown / Failed", message.BadgeLabel);
        Assert.Contains("duplicate", message.StatusHint);
        owner.TryClose();
    }
}
