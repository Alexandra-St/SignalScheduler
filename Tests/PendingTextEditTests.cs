using SignalScheduler.Models;
using SignalScheduler.Persistence;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class PendingTextEditTests
{
    [Fact]
    public async Task DispatchUsesEntireEditedTextExactlyOnce()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromSeconds(-1));
        const string body = "First line\nSecond line\nThird line\n\nLast line ♥";
        Assert.True(queue.Store.TryEditPendingText(message.Id, body));
        var sender = new FakeSignalSender();
        var dispatcher = new SignalScheduler.Services.MessageDispatcher(queue.Store, sender);
        await dispatcher.DispatchDueAsync();
        await dispatcher.DispatchDueAsync();
        Assert.Equal(body, Assert.Single(sender.Attempts).Text);
        Assert.Equal(MessageStatus.Sent, Assert.Single(queue.Store.Items).State);
    }

    [Fact]
    public void FailedWriteDoesNotPublishEditedText()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromHours(1));
        Directory.CreateDirectory(Path.Combine(queue.DirectoryPath, "queue.enc.tmp"));
        Assert.Throws<UnauthorizedAccessException>(() => queue.Store.TryEditPendingText(message.Id, "Changed"));
        Assert.Equal(message, Assert.Single(queue.Store.Items));
    }

    [Fact]
    public void EditPreservesIdentityScheduleAndAttachmentsAndSurvivesReload()
    {
        using var queue = new TestQueue();
        var original = queue.Add(TimeSpan.FromHours(2)) with
        { Attachments = new() { new("synthetic.png", new byte[] { 1, 2, 3 }) } };
        queue.Store.Remove(original.Id);
        queue.Store.Add(original);
        const string text = "Entire message\nSecond line\n\nLast line: привет ♥";
        Assert.True(queue.Store.TryEditPendingText(original.Id, text));
        var edited = Assert.Single(queue.Store.Items);
        Assert.Equal(original with { Text = text }, edited);
        queue.Store.Dispose();
        using var reopened = new EncryptedQueueStore(queue.DirectoryPath, queue.Key.ToArray());
        var saved = Assert.Single(reopened.Items);
        Assert.Equal(text, saved.Text);
        Assert.Equal(original.Id, saved.Id);
        Assert.Equal(original.Due, saved.Due);
        Assert.Equal(original.Account, saved.Account);
        Assert.Equal(original.Cli, saved.Cli);
        Assert.Equal(original.Recipient, saved.Recipient);
        Assert.Equal(MessageStatus.Pending, saved.State);
        Assert.Equal(original.Attachments![0].Data, saved.Attachments![0].Data);
    }

    [Theory]
    [InlineData(MessageStatus.Sending)]
    [InlineData(MessageStatus.Sent)]
    [InlineData(MessageStatus.Cancelled)]
    [InlineData(MessageStatus.Missed)]
    [InlineData(MessageStatus.Blocked)]
    [InlineData(MessageStatus.Unknown)]
    [InlineData(MessageStatus.UnknownOrFailed)]
    public void StatusChangeBeforeSavingRejectsEdit(MessageStatus state)
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromHours(1));
        queue.Store.ChangeStatus(message.Id, state);
        Assert.False(queue.Store.TryEditPendingText(message.Id, "Changed"));
        Assert.Equal(message.Text, Assert.Single(queue.Store.Items).Text);
        Assert.Equal(state, Assert.Single(queue.Store.Items).State);
    }

    [Fact]
    public void EmptyMessageWithoutPhotoAndMissingIdAreRejected()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromHours(1));
        Assert.False(queue.Store.TryEditPendingText(message.Id, " \n"));
        Assert.False(queue.Store.TryEditPendingText(Guid.NewGuid(), "Changed"));
        Assert.Equal(message, Assert.Single(queue.Store.Items));
    }
}
