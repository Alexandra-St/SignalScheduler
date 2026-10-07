using System.Text.Json;

namespace SignalScheduler.Infrastructure;

public static class SignalCliPreferences
{
    private static string FilePath => Path.Combine(MacApplicationPaths.DataDirectory, "signal-cli-settings.json");
    public static string? Load() => File.Exists(FilePath)
        ? JsonSerializer.Deserialize<Preference>(File.ReadAllText(FilePath))?.Executable : null;
    public static void Save(string? executable)
    {
        Directory.CreateDirectory(MacApplicationPaths.DataDirectory);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new Preference(executable)));
        File.Move(temporary, FilePath, true);
    }
    private sealed record Preference(string? Executable);
}
