namespace SignalScheduler.Models;

// Property names and string states form the existing queue format; do not rename them.
public sealed record ScheduledMessage(
    Guid Id,
    string Recipient,
    string Text,
    DateTimeOffset Due,
    string State,
    string Account,
    string Cli,
    List<ImageAttachment>? Attachments = null);
