using SignalScheduler.Models;

namespace SignalScheduler.Integrations.Signal;

// Resolve current settings rather than launching historical paths saved in the queue.
public sealed class ConfiguredSignalSender(Func<Task<SignalExecutable?>> resolve) : ISignalSender
{
    public async Task<(int Code, string Output)> SendAsync(ScheduledMessage message)
    {
        var executable = await resolve();
        if (executable == null) throw new IOException("signal-cli is unavailable.");
        return await new SignalCliAdapter().SendAsync(message with { Cli = executable.Path });
    }
}
