using SignalScheduler.Models;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class QueuePersistenceFailureTests
{
    [Fact]
    public void FailedAddDoesNotPublishMessageInMemory()
    {
        using var queue = new TestQueue();
        var message = new ScheduledMessage(Guid.NewGuid(), "recipient-placeholder", "Synthetic",
            DateTimeOffset.UtcNow.AddHours(1), MessageStatus.Pending, "account-placeholder", queue.Executable);
        BlockTemporaryFilePath(queue.DirectoryPath);

        Assert.Throws<UnauthorizedAccessException>(() => queue.Store.Add(message));

        Assert.Empty(queue.Store.Items);
    }

    [Fact]
    public void FailedRemoveKeepsMessageInMemory()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromHours(1));
        BlockTemporaryFilePath(queue.DirectoryPath);

        Assert.Throws<UnauthorizedAccessException>(() => queue.Store.Remove(message.Id));

        Assert.Equal(message, Assert.Single(queue.Store.Items));
    }

    [Fact]
    public void FailedStatusChangeKeepsPreviousStatusInMemory()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromHours(1));
        BlockTemporaryFilePath(queue.DirectoryPath);

        Assert.Throws<UnauthorizedAccessException>(() => queue.Store.ChangeStatus(message.Id, MessageStatus.Cancelled));

        Assert.Equal(message, Assert.Single(queue.Store.Items));
    }

    [Fact]
    public async Task FailedSendingStatusPersistenceDoesNotCallSenderOrChangeMemory()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromSeconds(-1));
        BlockTemporaryFilePath(queue.DirectoryPath);
        var sender = new FakeSignalSender();
        var dispatcher = new SignalScheduler.Services.MessageDispatcher(queue.Store, sender);

        await dispatcher.DispatchDueAsync();

        Assert.Empty(sender.Attempts);
        Assert.Equal(message, Assert.Single(queue.Store.Items));
    }

    private static void BlockTemporaryFilePath(string directory)
        => Directory.CreateDirectory(Path.Combine(directory, "queue.enc.tmp"));
}
