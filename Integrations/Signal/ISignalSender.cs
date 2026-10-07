using SignalScheduler.Models;

namespace SignalScheduler.Integrations.Signal;

// The dispatcher can be verified without linking an account or sending a real message.
public interface ISignalSender
{
    Task<(int Code, string Output)> SendAsync(ScheduledMessage message);
}
