using System.Text.RegularExpressions;
using SignalScheduler.Infrastructure;

namespace SignalScheduler.Integrations.Signal;

public sealed record SignalExecutable(string Path, string Version)
{
    public static async Task<SignalExecutable?> ValidateAsync(string path)
    {
        try
        {
            if (!System.IO.Path.IsPathFullyQualified(path) || !File.Exists(path)) return null;
            var mode = File.GetUnixFileMode(path);
            if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0) return null;
            var result = await ProcessRunner.RunAsync(path, new[] { "--version" }, timeoutSeconds: 10);
            var match = Regex.Match(result.Output.Trim(), @"^signal-cli\s+(\d+\.\d+\.\d+[^\s]*)$", RegexOptions.CultureInvariant);
            return result.Code == 0 && match.Success ? new(path, match.Groups[1].Value) : null;
        }
        catch { return null; }
    }

    public static IEnumerable<string> Candidates(string? path, IEnumerable<string>? fallback = null)
    {
        var directories = (path ?? "").Split(System.IO.Path.PathSeparator)
            .Where(System.IO.Path.IsPathFullyQualified);
        return directories.Select(directory => System.IO.Path.Combine(directory, "signal-cli"))
            .Concat(fallback ?? new[] { "/opt/homebrew/bin/signal-cli", "/usr/local/bin/signal-cli" })
            .Distinct(StringComparer.Ordinal);
    }

    public static async Task<SignalExecutable?> DetectAsync(string? path, IEnumerable<string>? fallback = null)
    {
        foreach (var candidate in Candidates(path, fallback))
            if (await ValidateAsync(candidate) is { } executable) return executable;
        return null;
    }
}
