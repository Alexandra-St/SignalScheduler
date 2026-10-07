using System.Text.Json;
using SignalScheduler.Infrastructure;

namespace SignalScheduler.Integrations.Signal;

public static class SignalAccountDiscovery
{
    public static async Task<IReadOnlyList<string>> ListAsync(string executable)
    {
        var result = await ProcessRunner.RunAsync(executable, new[] { "--output", "json", "listAccounts" });
        if (result.Code != 0)
            throw new IOException("Could not list linked accounts.");
        return ParseAccounts(result.Output);
    }

    public static IReadOnlyList<string> ParseAccounts(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Invalid account list.");
        var accounts = new List<string>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new JsonException("Invalid account entry.");
            var identifier = ReadString(item, "number") ?? ReadString(item, "aci");
            if (identifier is null)
                throw new JsonException("Missing account identifier.");
            if (!accounts.Contains(identifier, StringComparer.Ordinal)) accounts.Add(identifier);
        }
        return accounts;
    }

    private static string? ReadString(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
}
