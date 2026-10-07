using System.Text.Json;
using System.Text.Json.Serialization;
using SignalScheduler.Models;

namespace SignalScheduler.Persistence;

// Keep the original queue's string values, including explanatory text.
public sealed class MessageStatusJsonConverter : JsonConverter<MessageStatus>
{
    public override MessageStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString() switch
            {
                "Pending" => MessageStatus.Pending,
                "Sending" => MessageStatus.Sending,
                "Cancelled" => MessageStatus.Cancelled,
                "Sent" or "Sent — accepted by signal-cli" => MessageStatus.Sent,
                "Unknown" or "Unknown — check Signal before rescheduling" => MessageStatus.Unknown,
                "UnknownOrFailed" or "Unknown/failed — check Signal before rescheduling" => MessageStatus.UnknownOrFailed,
                "Missed" or "Missed — reschedule manually" => MessageStatus.Missed,
                "Blocked" or "Blocked — signal-cli missing; reschedule manually" => MessageStatus.Blocked,
                _ => MessageStatus.Unknown
            };

        // Invalid status values must not make an otherwise readable queue unusable.
        // Consume structured values too; never interpret numeric values as enum ordinals.
        using var ignored = JsonDocument.ParseValue(ref reader);
        return MessageStatus.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, MessageStatus value, JsonSerializerOptions options)
        => writer.WriteStringValue(value switch
        {
            MessageStatus.Pending => "Pending",
            MessageStatus.Sending => "Sending",
            MessageStatus.Cancelled => "Cancelled",
            MessageStatus.Sent => "Sent — accepted by signal-cli",
            MessageStatus.UnknownOrFailed => "Unknown/failed — check Signal before rescheduling",
            MessageStatus.Missed => "Missed — reschedule manually",
            MessageStatus.Blocked => "Blocked — signal-cli missing; reschedule manually",
            _ => "Unknown — check Signal before rescheduling"
        });
}
