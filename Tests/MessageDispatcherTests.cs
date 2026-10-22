using SignalScheduler.Integrations.Signal;
using SignalScheduler.Models;
using SignalScheduler.Services;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class MessageDispatcherTests
{
    [Fact]
    public async Task FutureAndCancelledMessagesAreNotAttempted()
    {
        using var queue = new TestQueue();
        queue.Add(TimeSpan.FromMinutes(10));
        queue.Add(TimeSpan.FromMinutes(-1), MessageStatus.Cancelled);
        var sender = new FakeSignalSender();
        await new MessageDispatcher(queue.Store, sender).DispatchDueAsync();
        Assert.Empty(sender.Attempts);
    }

    [Fact]
    public async Task PersistSendingBeforeAttemptAndAcceptedAfterward()
    {
        using var queue = new TestQueue();
        queue.Add(TimeSpan.FromMinutes(-1));
        var sender = new FakeSignalSender
        {
            Send = _ =>
            {
                Assert.Equal(MessageStatus.Sending, Assert.Single(queue.Store.Items).State);
                return Task.FromResult(new SignalSendResult(0));
            }
        };
        var dispatcher = new MessageDispatcher(queue.Store, sender);
        DispatchOutcome? outcome = null;
        dispatcher.SendingCompleted += value => outcome = value;
        await dispatcher.DispatchDueAsync();
        await dispatcher.DispatchDueAsync();
        Assert.Single(sender.Attempts);
        Assert.Equal(MessageStatus.Sent, Assert.Single(queue.Store.Items).State);
        Assert.Equal(DispatchOutcome.Accepted, outcome);
    }

    [Fact]
    public async Task MoreThanFiveMinutesLateIsMissedWithoutSending()
    {
        using var queue = new TestQueue();
        queue.Add(TimeSpan.FromMinutes(-6));
        var sender = new FakeSignalSender();
        await new MessageDispatcher(queue.Store, sender).DispatchDueAsync();
        Assert.Empty(sender.Attempts);
        Assert.Equal(MessageStatus.Missed, Assert.Single(queue.Store.Items).State);
    }

    [Fact]
    public async Task MissingExecutableIsBlockedWithoutAttempt()
    {
        using var queue = new TestQueue();
        queue.Add(TimeSpan.FromMinutes(-1));
        File.Delete(queue.Executable);
        var sender = new FakeSignalSender();
        await new MessageDispatcher(queue.Store, sender).DispatchDueAsync();
        Assert.Empty(sender.Attempts);
        Assert.Equal(MessageStatus.Blocked, Assert.Single(queue.Store.Items).State);
    }

    [Theory]
    [InlineData(false, MessageStatus.UnknownOrFailed, DispatchOutcome.Uncertain)]
    [InlineData(true, MessageStatus.Unknown, DispatchOutcome.Interrupted)]
    public async Task UncertainAndInterruptedAttemptsAreNeverRetried(bool throws, MessageStatus state, DispatchOutcome expected)
    {
        using var queue = new TestQueue();
        queue.Add(TimeSpan.FromMinutes(-1));
        var sender = new FakeSignalSender
        {
            Send = _ => throws ? Task.FromException<SignalSendResult>(new IOException("Synthetic failure"))
                : Task.FromResult(new SignalSendResult(1))
        };
        var dispatcher = new MessageDispatcher(queue.Store, sender);
        DispatchOutcome? outcome = null;
        dispatcher.SendingCompleted += value => outcome = value;
        await dispatcher.DispatchDueAsync();
        await dispatcher.DispatchDueAsync();
        Assert.Single(sender.Attempts);
        Assert.Equal(state, Assert.Single(queue.Store.Items).State);
        Assert.Equal(expected, outcome);
        Assert.False(dispatcher.IsBusy);
    }

    [Theory]
    [InlineData(4, true, MessageStatus.RequiresVerification, DispatchOutcome.RequiresVerification)]
    [InlineData(4, false, MessageStatus.UnknownOrFailed, DispatchOutcome.Uncertain)]
    [InlineData(3, false, MessageStatus.UnknownOrFailed, DispatchOutcome.Uncertain)]
    public async Task OnlyCompatibleUntrustedIdentityResultRequiresVerification(int code, bool requiresVerification,
        MessageStatus expectedState, DispatchOutcome expectedOutcome)
    {
        using var queue = new TestQueue();
        queue.Add(TimeSpan.FromMinutes(-1));
        var sender = new FakeSignalSender
        {
            Send = _ => Task.FromResult(new SignalSendResult(code, requiresVerification))
        };
        var dispatcher = new MessageDispatcher(queue.Store, sender);
        DispatchOutcome? outcome = null;
        dispatcher.SendingCompleted += value => outcome = value;

        await dispatcher.DispatchDueAsync();
        await dispatcher.DispatchDueAsync();

        Assert.Single(sender.Attempts);
        Assert.Equal(expectedState, Assert.Single(queue.Store.Items).State);
        Assert.Equal(expectedOutcome, outcome);
    }

    [Fact]
    public async Task OverlappingTicksDoNotStartConcurrentSends()
    {
        using var queue = new TestQueue();
        queue.Add(TimeSpan.FromMinutes(-2));
        queue.Add(TimeSpan.FromMinutes(-1));
        var release = new TaskCompletionSource<SignalSendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new FakeSignalSender { Send = _ => release.Task };
        var dispatcher = new MessageDispatcher(queue.Store, sender);
        var first = dispatcher.DispatchDueAsync();
        Assert.True(dispatcher.IsBusy);
        await dispatcher.DispatchDueAsync();
        Assert.Single(sender.Attempts);
        release.SetResult(new SignalSendResult(0));
        await first;
        Assert.False(dispatcher.IsBusy);
        Assert.Single(queue.Store.Items, message => message.State == MessageStatus.Pending);
    }

    [Fact]
    public async Task DispatchOrderIsOldestDueFirstAndClosingStopsNewAttempts()
    {
        using var queue = new TestQueue();
        var newer = queue.Add(TimeSpan.FromMinutes(-1));
        var older = queue.Add(TimeSpan.FromMinutes(-2));
        var sender = new FakeSignalSender();
        var dispatcher = new MessageDispatcher(queue.Store, sender);
        await dispatcher.DispatchDueAsync();
        Assert.Equal(older.Id, Assert.Single(sender.Attempts).Id);
        dispatcher.Close();
        await dispatcher.DispatchDueAsync();
        Assert.Single(sender.Attempts);
        Assert.Equal(MessageStatus.Pending, queue.Store.Items.Single(message => message.Id == newer.Id).State);
    }
}
