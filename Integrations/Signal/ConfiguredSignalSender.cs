using SignalScheduler.Models;

namespace SignalScheduler.Integrations.Signal;

// Resolve current settings rather than launching historical paths saved in the queue.
public sealed class ConfiguredSignalSender(Func<Task<SignalExecutable?>> resolve) : ISignalSender
{
    private const int UntrustedIdentityExitCode = 4;

    public async Task<SignalSendResult> SendAsync(ScheduledMessage message)
    {
        var executable = await resolve();
        if (executable == null) throw new IOException("signal-cli is unavailable.");
        var result = await new SignalCliAdapter().SendAsync(message with { Cli = executable.Path });
        return new SignalSendResult(result.Code,
            executable.Version == "0.14.9" && result.Code == UntrustedIdentityExitCode);
    }
}
