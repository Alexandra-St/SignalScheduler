using System.Diagnostics;

namespace SignalScheduler.Infrastructure;

public static class ProcessRunner
{
    public static async Task<(int Code, string Output)> RunAsync(
        string executable, IEnumerable<string> arguments, string? input = null, int timeoutSeconds = 90)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new IOException("Could not start process");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (input != null) await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch
        {
            try { process.Kill(true); } catch { }
            throw;
        }

        await error; // Do not retain diagnostics that may include message contents.
        return (process.ExitCode, await output);
    }
}
