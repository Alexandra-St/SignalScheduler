using SignalScheduler.Infrastructure;
using SignalScheduler.Models;

namespace SignalScheduler.Integrations.Signal;

public sealed class SignalCliAdapter : ISignalSender
{
    public async Task<(int Code, string Output)> SendAsync(ScheduledMessage message)
    {
        if (message.Attachments == null || message.Attachments.Count == 0)
            return await ProcessRunner.RunAsync(message.Cli,
                new[] { "-a", message.Account, "send", "--message-from-stdin", message.Recipient }, message.Text);

        var folder = MacApplicationPaths.CreateTemporaryDirectory();
        try
        {
            var arguments = new List<string>
            {
                "-a", message.Account, "send", "--message-from-stdin", message.Recipient, "--attachment"
            };
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
