using SignalScheduler.Models;
using SignalScheduler.Persistence;
using SignalScheduler.Services;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class RecoveryTests
{
    [Fact]
    public async Task InterruptedSendingRecoversAsUnknownAndNeverRetriesAcrossRestarts()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromMinutes(-1));
        // The durable state left by a process that stopped during SendAsync.
        queue.Store.ChangeStatus(message.Id, MessageStatus.Sending);
        queue.Store.Dispose();

        for (var restart = 0; restart < 2; restart++)
        {
            using var reopened = new EncryptedQueueStore(queue.DirectoryPath, queue.Key.ToArray());
            Assert.Equal(MessageStatus.Unknown, Assert.Single(reopened.Items).State);
            var sender = new FakeSignalSender();
            var dispatcher = new MessageDispatcher(reopened, sender);
            await dispatcher.DispatchDueAsync();
            await dispatcher.DispatchDueAsync();
            Assert.Empty(sender.Attempts);
        }
    }

    [Theory]
    [InlineData(false, MessageStatus.UnknownOrFailed)]
    [InlineData(true, MessageStatus.Unknown)]
    public async Task UncertainResultPersistsWithoutRetryButOtherPendingMessagesStillDispatch(
        bool throws, MessageStatus expected)
    {
        using var queue = new TestQueue();
        var uncertain = queue.Add(TimeSpan.FromMinutes(-2));
        var next = queue.Add(TimeSpan.FromMinutes(-1));
        var sender = new FakeSignalSender
        {
            Send = _ => throws
                ? Task.FromException<(int, string)>(new IOException("Synthetic interrupted response"))
                : Task.FromResult((1, "Synthetic uncertain response"))
        };
        await new MessageDispatcher(queue.Store, sender).DispatchDueAsync();
        Assert.Equal(uncertain.Id, Assert.Single(sender.Attempts).Id);
        queue.Store.Dispose();

        using (var reopened = new EncryptedQueueStore(queue.DirectoryPath, queue.Key.ToArray()))
        {
            Assert.Equal(expected, reopened.Items.Single(item => item.Id == uncertain.Id).State);
            var afterRestart = new FakeSignalSender();
            var dispatcher = new MessageDispatcher(reopened, afterRestart);
            await dispatcher.DispatchDueAsync();
            await dispatcher.DispatchDueAsync();
            Assert.Equal(next.Id, Assert.Single(afterRestart.Attempts).Id);
        }

        using var final = new EncryptedQueueStore(queue.DirectoryPath, queue.Key.ToArray());
        Assert.Equal(expected, final.Items.Single(item => item.Id == uncertain.Id).State);
        Assert.Equal(MessageStatus.Sent, final.Items.Single(item => item.Id == next.Id).State);
    }
}
