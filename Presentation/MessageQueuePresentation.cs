using SignalScheduler.Models;
using SignalScheduler.ViewModels;

namespace SignalScheduler.Presentation;

public sealed record MessageGroup(string Title, IReadOnlyList<MessageViewModel> Items);

public static class MessageQueuePresentation
{
    public static IReadOnlyList<MessageGroup> Upcoming(IEnumerable<MessageViewModel> messages, DateTime today)
        => messages.Where(m => m.Message.State is MessageStatus.Pending or MessageStatus.Sending)
            .OrderBy(m => m.Message.Due)
            .GroupBy(m => m.Message.Due.LocalDateTime.Date <= today.Date ? "Today"
                : m.Message.Due.LocalDateTime.Date == today.Date.AddDays(1) ? "Tomorrow" : "Later")
            .Select(group => new MessageGroup(group.Key, group.ToArray())).ToArray();

    public static IReadOnlyList<MessageViewModel> History(IEnumerable<MessageViewModel> messages, string filter)
        => messages.Where(m => m.Message.State is not (MessageStatus.Pending or MessageStatus.Sending))
            .Where(m => filter switch
            {
                "Sent" => m.Message.State == MessageStatus.Sent,
                "Cancelled" => m.Message.State == MessageStatus.Cancelled,
                "Missed" => m.Message.State == MessageStatus.Missed,
                "Unknown / Failed" => m.Message.State is MessageStatus.Unknown or MessageStatus.UnknownOrFailed,
                "Blocked" => m.Message.State == MessageStatus.Blocked,
                _ => true
            }).OrderByDescending(m => m.Message.Due).ToArray();
}
