using System.Text.Json;
using SignalScheduler.Infrastructure;
using SignalScheduler.Models;

namespace SignalScheduler.Integrations.Signal;

public sealed class SignalCliAdapter
{
    private readonly Action<string, bool> deleteDirectory;

    public SignalCliAdapter() : this(Directory.Delete) { }

    internal SignalCliAdapter(Action<string, bool> deleteDirectory)
    {
        this.deleteDirectory = deleteDirectory;
    }

    public async Task<IReadOnlyList<SignalIdentitySnapshot>> ListIdentitiesAsync(
        string executable, string account, string recipient)
    {
        var result = await ProcessRunner.RunAsync(executable,
            new[] { "--output", "json", "-a", account, "listIdentities", "-n", recipient });
        if (result.Code != 0) throw new IOException("Could not retrieve Signal identity.");

        try
        {
            using var document = JsonDocument.Parse(result.Output);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new JsonException();
            return document.RootElement.EnumerateArray().Select(item => new SignalIdentitySnapshot(
                item.GetProperty("trustLevel").GetString() ?? "",
                item.GetProperty("safetyNumber").GetString() ?? ""))
                .Where(identity => identity.TrustStatus.Length > 0 && identity.SafetyNumber.Length > 0)
                .ToArray();
        }
        catch (JsonException) { throw new IOException("Could not read Signal identity details."); }
        catch (InvalidOperationException) { throw new IOException("Could not read Signal identity details."); }
        catch (KeyNotFoundException) { throw new IOException("Could not read Signal identity details."); }
    }

    public async Task TrustVerifiedIdentityAsync(string executable, string account, string recipient, string safetyNumber)
    {
        var result = await ProcessRunner.RunAsync(executable,
            new[] { "-a", account, "trust", "--verified-safety-number", safetyNumber, recipient });
        if (result.Code != 0) throw new IOException("Signal could not verify this identity.");
    }

    public async Task<(int Code, string Output)> SendAsync(ScheduledMessage message)
    {
        if (!SignalRecipient.TryParse(message.Recipient, out var recipient))
            throw new ArgumentException(SignalRecipient.Error);
        var arguments = new List<string> { "-a", message.Account, "send", "--message-from-stdin" };
        if (recipient!.UseUsername) arguments.Add("--username");
        arguments.Add(recipient.Value);
        if (message.Attachments == null || message.Attachments.Count == 0)
            return await ProcessRunner.RunAsync(message.Cli, arguments, message.Text);

        var folder = MacApplicationPaths.CreateTemporaryDirectory();
        try
        {
            arguments.Add("--attachment");
            for (var i = 0; i < message.Attachments.Count; i++)
            {
                var attachment = message.Attachments[i];
                var path = Path.Combine(folder, i + "-" + Path.GetFileName(attachment.Name));
                await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    await file.WriteAsync(attachment.Data);
                }
                arguments.Add(path);
            }
            return await ProcessRunner.RunAsync(message.Cli, arguments, message.Text);
        }
        finally
        {
            try { deleteDirectory(folder, true); }
            catch (Exception) { /* Temporary-file cleanup must not replace the send result. */ }
        }
    }
}

public sealed record SignalIdentitySnapshot(string TrustStatus, string SafetyNumber);
