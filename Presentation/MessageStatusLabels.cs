using SignalScheduler.Models;

namespace SignalScheduler.Presentation;

internal static class MessageStatusLabels
{
    public static string Format(MessageStatus status) => status switch
    {
        MessageStatus.Pending => "Pending",
        MessageStatus.Sending => "Sending",
        MessageStatus.Cancelled => "Canceled",
        MessageStatus.Sent => "Sent",
        MessageStatus.UnknownOrFailed => "Unknown/failed",
        MessageStatus.Missed => "Missed",
        MessageStatus.Blocked => "Blocked",
        _ => "Unknown"
    };

    public static string? Hint(MessageStatus status) => status switch
    {
        MessageStatus.Unknown or MessageStatus.UnknownOrFailed => "Check Signal before scheduling again to avoid sending a duplicate.",
        MessageStatus.Missed => "The scheduled time was missed. Choose a new time to send this message.",
        MessageStatus.Blocked => "signal-cli was unavailable. Check Settings, then choose a new send time.",
        _ => null
    };
}
