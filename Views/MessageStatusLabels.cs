using SignalScheduler.Models;

namespace SignalScheduler.Views;

internal static class MessageStatusLabels
{
    public static string Format(MessageStatus status) => status switch
    {
        MessageStatus.Pending => "Pending",
        MessageStatus.Sending => "Sending",
        MessageStatus.Cancelled => "Cancelled",
        MessageStatus.Sent => "Sent — accepted by signal-cli",
        MessageStatus.UnknownOrFailed => "Unknown/failed — check Signal before rescheduling",
        MessageStatus.Missed => "Missed — reschedule manually",
        MessageStatus.Blocked => "Blocked — signal-cli missing; reschedule manually",
        _ => "Unknown — check Signal before rescheduling"
    };
}
