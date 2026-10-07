using System.Globalization;
using System.Text.RegularExpressions;

namespace SignalScheduler.Presentation;

public static class SendTimeValidation
{
    public static bool TryGetDue(string? text, DateTimeOffset now, TimeZoneInfo zone,
        out DateTimeOffset due, out string error)
    {
        due = default;
        if (string.IsNullOrEmpty(text))
        { error = "Enter a send date and time."; return false; }
        if (!Regex.IsMatch(text, @"\A[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}\z") ||
            !DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        { error = "Enter a valid date and time in yyyy-MM-dd HH:mm format."; return false; }
        if (zone.IsInvalidTime(date))
        { error = "This time is skipped by daylight saving. Choose another time."; return false; }
        if (zone.IsAmbiguousTime(date))
        { error = "This time occurs twice due to daylight saving. Choose another time."; return false; }
        due = new DateTimeOffset(date, zone.GetUtcOffset(date));
        if (due <= now)
        { error = "Choose a future date and time."; return false; }
        error = "";
        return true;
    }
}
