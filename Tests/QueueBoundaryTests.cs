using SignalScheduler.Models;
using SignalScheduler.Persistence;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class QueueBoundaryTests
{
    [Fact]
    public void MutationsPersistWithoutAnExplicitSaveAndItemsCannotBeModified()
    {
        using var queue = new TestQueue();
        var removed = queue.Add(TimeSpan.FromHours(1));
        var kept = queue.Add(TimeSpan.FromHours(2));
        Assert.Throws<NotSupportedException>(() => ((IList<ScheduledMessage>)queue.Store.Items).Clear());
        queue.Store.ChangeStatus(kept.Id, MessageStatus.Cancelled);
        queue.Store.Remove(removed.Id);
        queue.Store.Dispose();
        using var reopened = new EncryptedQueueStore(queue.DirectoryPath, queue.Key.ToArray());
        var loaded = Assert.Single(reopened.Items);
        Assert.Equal(kept.Id, loaded.Id);
        Assert.Equal(MessageStatus.Cancelled, loaded.State);
    }
}
