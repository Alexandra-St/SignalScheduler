using System.Security.Cryptography;
using SignalScheduler.Infrastructure;
using SignalScheduler.Integrations.Signal;
using SignalScheduler.Models;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class SignalCliAdapterTests
{
    [Fact]
    public async Task ProcessRunnerPassesTextThroughStdinWithoutShellExpansion()
    {
        const string text = "Synthetic `literal` $(literal) text\nsecond line";
        var result = await ProcessRunner.RunAsync("/bin/cat", Array.Empty<string>(), text);
        Assert.Equal(0, result.Code);
        Assert.Equal(text, result.Output);
    }

    [Fact]
    public async Task AdapterPreservesArgumentsAttachmentBytesAndTemporaryCleanup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "signal-scheduler-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, "fake-cli");
        try
        {
            // Print argv and the attachment's hash, then copy stdin. No Signal account is used.
            await File.WriteAllTextAsync(executable,
                "#!/bin/sh\nprintf '%s\\n' \"$@\"\ncat \"$7\" | shasum -a 256\ncat\n");
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var bytes = new byte[] { 1, 2, 3, 4 };
            var message = new ScheduledMessage(Guid.NewGuid(), "+12025550123", "Synthetic caption",
                DateTimeOffset.UtcNow, MessageStatus.Pending, "account-placeholder", executable,
                new() { new ImageAttachment("synthetic image.png", bytes) });
            var result = await new SignalCliAdapter().SendAsync(message);
            Assert.Equal(0, result.Code);
            var lines = result.Output.Split('\n');
            Assert.Equal(new[] { "-a", "account-placeholder", "send", "--message-from-stdin", "+12025550123", "--attachment" }, lines[..6]);
            Assert.EndsWith("0-synthetic image.png", lines[6]);
            Assert.StartsWith(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), lines[7]);
            Assert.EndsWith(message.Text, result.Output);
            Assert.False(File.Exists(lines[6]));
            Assert.False(Directory.Exists(Path.GetDirectoryName(lines[6])));
        }
        finally { Directory.Delete(directory, true); }
    }
}
