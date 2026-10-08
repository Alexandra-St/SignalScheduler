using System.Text.RegularExpressions;

namespace SignalScheduler.Integrations.Signal;

public sealed record SignalRecipient(string Value, bool UseUsername)
{
    public const string Error = "Enter a phone number, Signal username, or signal.me link.";

    public static bool TryParse(string? input, out SignalRecipient? recipient)
    {
        recipient = null;
        var value = (input ?? "").Trim();
        if (Regex.IsMatch(value, @"\A\+[1-9][0-9]{6,14}\z"))
        { recipient = new(value, false); return true; }
        if (Regex.IsMatch(value, @"\A[A-Za-z_][A-Za-z0-9_]{2,31}\.[0-9]{2,9}\z") &&
            value[(value.LastIndexOf('.') + 1)..].Any(character => character != '0'))
        { recipient = new(value, true); return true; }
        // Treat the link payload as opaque; signal-cli resolves the recipient.
        if (Regex.IsMatch(value, @"\Ahttps://signal\.me/#eu/[A-Za-z0-9_-]+={0,2}\z", RegexOptions.IgnoreCase))
        { recipient = new(value, true); return true; }
        return false;
    }
}
