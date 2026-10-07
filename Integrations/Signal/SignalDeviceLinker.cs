using System.Diagnostics;

namespace SignalScheduler.Integrations.Signal;

public static class SignalDeviceLinker
{
    public static async Task LinkAsync(string executable, Func<string, Task> showCode,
        CancellationToken cancellationToken, int timeoutSeconds = 180)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        foreach (var argument in new[] { "link", "-n", "Signal Scheduler" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Could not start Signal connection.");
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var codeSeen = 0;
        async Task ReadAsync(StreamReader stream)
        {
            try
            {
                while (await stream.ReadLineAsync(lifetime.Token) is { } line)
                {
                    if (TryReadLinkUri(line, out var uri) && Interlocked.Exchange(ref codeSeen, 1) == 0)
                        await showCode(uri);
                    // Never retain subprocess output: it may contain linking secrets or account identifiers.
                }
            }
            catch
            {
                lifetime.Cancel();
                throw;
            }
        }
        var stdout = ReadAsync(process.StandardOutput);
        var stderr = ReadAsync(process.StandardError);
        try
        {
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(lifetime.Token));
            cancellationToken.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || codeSeen == 0)
                throw new IOException("Could not complete Signal connection.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Signal connection expired.");
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                await process.WaitForExitAsync();
            }
        }
    }

    public static bool TryReadLinkUri(string line, out string link)
    {
        link = "";
        var text = line.Trim();
        if (text.Length > 4096 || !Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || uri.Scheme != "sgnl" || uri.Host != "linkdevice" || uri.AbsolutePath is not ("" or "/")
            || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0 || uri.Port != -1) return false;
        var fields = uri.Query.TrimStart('?').Split('&').Select(item => item.Split('=', 2)).ToList();
        if (!fields.Any(field => field.Length == 2 && field[0] == "uuid" && field[1].Length > 0)
            || !fields.Any(field => field.Length == 2 && field[0] == "pub_key" && field[1].Length > 0)) return false;
        link = text;
        return true;
    }
}
