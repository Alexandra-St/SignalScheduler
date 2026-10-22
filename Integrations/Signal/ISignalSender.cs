using SignalScheduler.Models;

namespace SignalScheduler.Integrations.Signal;

// The dispatcher can be verified without linking an account or sending a real message.
public interface ISignalSender
{
    Task<SignalSendResult> SendAsync(ScheduledMessage message);
}

public sealed record SignalSendResult(int Code, bool RequiresVerification = false);
