#if MANUAL_ACCEPTANCE
using System.Security.Cryptography;
using SignalScheduler.Integrations.Signal;
using SignalScheduler.Models;
using SignalScheduler.Persistence;

namespace SignalScheduler.Infrastructure;

internal static class ManualAcceptanceProfile
{
    private const string ProfileVariable = "SIGNALSCHEDULER_MANUAL_ACCEPTANCE_PROFILE";
    private const string MarkerName = ".manual-acceptance-profile";

    public static string DataDirectory
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable(ProfileVariable);
            if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("Manual acceptance profile is not configured.");
            var path = Path.GetFullPath(raw);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(temp, StringComparison.Ordinal) || !Directory.Exists(path))
                throw new InvalidOperationException("Manual acceptance profile must be an existing directory under the system temporary folder.");
            var directory = new DirectoryInfo(path);
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0
                || !File.Exists(Path.Combine(path, MarkerName))
                || !File.Exists(Path.Combine(path, "fake-signal-cli")))
                throw new InvalidOperationException("Manual acceptance profile is not valid.");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return path;
        }
    }

    public static string FakeCliPath => Path.Combine(DataDirectory, "fake-signal-cli");

    public static async Task<SignalExecutable?> DetectExecutableAsync()
    {
        try { return await SignalExecutable.ValidateAsync(FakeCliPath); }
        catch { return null; }
    }

    public static byte[] GetOrCreateQueueKey()
    {
        var path = Path.Combine(DataDirectory, "queue-test.key");
        if (File.Exists(path))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Manual acceptance key file is invalid.");
            var existing = File.ReadAllBytes(path);
            if (existing.Length != 32) throw new IOException("Manual acceptance key file is invalid.");
            return existing;
        }

        var key = RandomNumberGenerator.GetBytes(32);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            stream.Write(key);
            stream.Flush(true);
        }
        return key;
    }

    public static void SeedQueueWhenEmpty(EncryptedQueueStore store)
    {
        if (store.Items.Count != 0) return;
        var now = DateTimeOffset.Now.AddHours(-1);
        store.Add(new ScheduledMessage(Guid.NewGuid(), "+12025550101", "Synthetic message for identity verification.",
            now, MessageStatus.RequiresVerification, "+12025550100", FakeCliPath));
        store.Add(new ScheduledMessage(Guid.NewGuid(), "+12025550102", "Second synthetic message for retry and cancellation checks.",
            now.AddMinutes(-5), MessageStatus.RequiresVerification, "+12025550100", FakeCliPath));
    }

    // The manual build never launches signal-cli for a send, even if a future message becomes due.
    internal sealed class NoNetworkSignalSender : ISignalSender
    {
        public Task<SignalSendResult> SendAsync(ScheduledMessage message)
            => Task.FromResult(new SignalSendResult(4, RequiresVerification: true));
    }
}
#endif
