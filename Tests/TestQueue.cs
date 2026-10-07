using System.Security.Cryptography;
using SignalScheduler.Integrations.Signal;
using SignalScheduler.Models;
using SignalScheduler.Persistence;

namespace SignalScheduler.Tests;

internal sealed class TestQueue : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "signal-scheduler-tests", Guid.NewGuid().ToString("N"));
    public byte[] Key { get; } = RandomNumberGenerator.GetBytes(32);
    public EncryptedQueueStore Store { get; }
    public string Executable { get; }

    public TestQueue()
    {
        Store = new EncryptedQueueStore(DirectoryPath, Key.ToArray());
        Executable = Path.Combine(DirectoryPath, "fake-cli");
        File.WriteAllText(Executable, "test fixture; not a Signal executable");
    }

    public ScheduledMessage Add(TimeSpan delay, MessageStatus state = MessageStatus.Pending)
    {
        var message = new ScheduledMessage(Guid.NewGuid(), "recipient-placeholder", "Synthetic test message",
            DateTimeOffset.UtcNow + delay, state, "account-placeholder", Executable);
        Store.Items.Add(message);
        Store.Save();
        return message;
    }

    public void Dispose()
    {
        Store.Dispose();
        Directory.Delete(DirectoryPath, true);
        CryptographicOperations.ZeroMemory(Key);
    }
}

internal sealed class FakeSignalSender : ISignalSender
{
    public List<ScheduledMessage> Attempts { get; } = new();
    public Func<ScheduledMessage, Task<(int Code, string Output)>> Send { get; set; }
        = _ => Task.FromResult((0, ""));

    public Task<(int Code, string Output)> SendAsync(ScheduledMessage message)
    {
        Attempts.Add(message);
        return Send(message);
    }
}
