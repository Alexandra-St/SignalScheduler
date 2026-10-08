using SignalScheduler.Integrations.Signal;
using SignalScheduler.Models;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class SignalRecipientTests
{
    [Theory]
    [InlineData("+12025550123", false)]
    [InlineData("Test_User.27", true)]
    [InlineData("https://signal.me/#eu/synthetic_opaque-link", true)]
    public async Task SupportedFormatsPreserveRecipientAndUseCorrectCliArguments(string input, bool username)
    {
        Assert.True(SignalRecipient.TryParse("  " + input + "  ", out var target));
        Assert.Equal(input, target!.Value);
        Assert.Equal(username, target.UseUsername);
        using var queue = new TestQueue();
        SignalDeviceLinkerTests.WriteExecutable(queue.Executable, "printf '%s\\n' \"$@\"\ncat\n");
        foreach (var attach in new[] { false, true })
        {
            var message = new ScheduledMessage(Guid.NewGuid(), input, "Synthetic", DateTimeOffset.UtcNow,
                MessageStatus.Pending, "synthetic-account", queue.Executable,
                attach ? new() { new ImageAttachment("synthetic.png", new byte[] { 1, 2 }) } : null);
            var result = await new SignalCliAdapter().SendAsync(message);
            Assert.Equal(0, result.Code);
            var lines = result.Output.Split('\n');
            Assert.Equal(new[] { "-a", "synthetic-account", "send", "--message-from-stdin" }, lines[..4]);
            Assert.Equal(username ? "--username" : input, lines[4]);
            var index = username ? 5 : 4;
            Assert.Equal(input, lines[index]);
            if (attach)
            {
                Assert.Equal("--attachment", lines[index + 1]);
                Assert.False(File.Exists(lines[index + 2]));
            }
            Assert.EndsWith("Synthetic", result.Output);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Test_User")]
    [InlineData("Test_User.2")]
    [InlineData("Test_User.00")]
    [InlineData("--username")]
    [InlineData("u:Test_User.27")]
    [InlineData("https://signal.me.evil.test/#eu/payload")]
    [InlineData("https://signal.me/#eu/")]
    [InlineData("https://signal.me/#eu/payload?extra")]
    [InlineData("https://signal.me/#eu/payload\n--config")]
    public void MalformedRecipientsAreRejected(string input)
        => Assert.False(SignalRecipient.TryParse(input, out _));
}
