using SignalScheduler.Integrations.Signal;
using SignalScheduler.Models;
using SignalScheduler.Persistence;

namespace SignalScheduler.Services;

public sealed class MessageDispatcher(EncryptedQueueStore store, ISignalSender sender)
{
    public bool IsBusy { get; private set; }
    public bool IsClosing { get; private set; }
    public bool IsFaulted { get; private set; }
    public event Action? SendingStarted;
    public event Action? QueueChanged;
    public event Action? QueueWriteFailed;
    public event Action<DispatchOutcome>? SendingCompleted;

    public void Close() => IsClosing = true;

    public async Task DispatchDueAsync()
    {
        if (IsBusy || IsClosing) return;
        var message = store.Items
            .Where(item => item.State == MessageStatus.Pending && item.Due <= DateTimeOffset.UtcNow)
            .OrderBy(item => item.Due).FirstOrDefault();
        if (message == null) return;
        if (DateTimeOffset.UtcNow - message.Due > TimeSpan.FromMinutes(5))
        {
            ChangeWithoutSending(message.Id, MessageStatus.Missed);
            return;
        }
        if (!File.Exists(message.Cli))
        {
            ChangeWithoutSending(message.Id, MessageStatus.Blocked);
            return;
        }

        IsBusy = true;
        SendingStarted?.Invoke();
        var started = false;
        var outcome = DispatchOutcome.Interrupted;
        try
        {
            store.Change(message.Id, MessageStatus.Sending);
            QueueChanged?.Invoke();
            started = true;
            var result = await sender.SendAsync(message);
            store.Change(message.Id, result.Code == 0 ? MessageStatus.Sent : MessageStatus.UnknownOrFailed);
            outcome = result.Code == 0 ? DispatchOutcome.Accepted : DispatchOutcome.Uncertain;
        }
        catch
        {
            try { if (started) store.Change(message.Id, MessageStatus.Unknown); }
            catch { IsFaulted = true; }
        }
        finally
        {
            IsBusy = false;
            SendingCompleted?.Invoke(outcome);
            QueueChanged?.Invoke();
        }
    }

    private void ChangeWithoutSending(Guid id, MessageStatus state)
    {
        try { store.Change(id, state); QueueChanged?.Invoke(); }
        catch { QueueWriteFailed?.Invoke(); }
    }
}
