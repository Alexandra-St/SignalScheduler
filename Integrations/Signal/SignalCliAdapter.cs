using SignalScheduler.Infrastructure;
using SignalScheduler.Models;

namespace SignalScheduler.Integrations.Signal;

public sealed class SignalCliAdapter : ISignalSender
{
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
        finally { Directory.Delete(folder, true); }
    }
}
