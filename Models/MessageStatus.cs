using System.Text.Json.Serialization;
using SignalScheduler.Persistence;

namespace SignalScheduler.Models;

[JsonConverter(typeof(MessageStatusJsonConverter))]
public enum MessageStatus
{
    Unknown = 0,
    Pending,
    Sending,
    Cancelled,
    Sent,
    UnknownOrFailed,
    Missed,
    Blocked
}
