namespace SignalScheduler.Models;

// Preserve persisted values and visible labels without a queue migration.
public static class MessageStates
{
    public const string Pending = "Pending";
    public const string Sending = "Sending";
    public const string Cancelled = "Cancelled";
    public const string Sent = "Sent — accepted by signal-cli";
    public const string Unknown = "Unknown — check Signal before rescheduling";
    public const string UnknownOrFailed = "Unknown/failed — check Signal before rescheduling";
    public const string Missed = "Missed — reschedule manually";
    public const string Blocked = "Blocked — signal-cli missing; reschedule manually";
}
