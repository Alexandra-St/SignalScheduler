namespace SignalScheduler.Models;

// Property names form the existing queue format; State uses a compatible string converter.
public sealed record ScheduledMessage(
    Guid Id,
    string Recipient,
    string Text,
    DateTimeOffset Due,
    MessageStatus State,
    string Account,
    string Cli,
    List<ImageAttachment>? Attachments = null);
