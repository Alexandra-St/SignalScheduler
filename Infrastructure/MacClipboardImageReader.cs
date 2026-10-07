using System.Text.Json;
using SignalScheduler.Models;

namespace SignalScheduler.Infrastructure;

public static class MacClipboardImageReader
{
    public static async Task<ImageAttachment> ReadAsync(int maxBytes)
    {
        var folder = MacApplicationPaths.CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(folder, "clipboard.png");
            // Keep the native PNG conversion used by the original screenshot workflow.
            var script = "set targetFile to POSIX file " + JsonSerializer.Serialize(path)
                + "\nset imageData to the clipboard as «class PNGf»"
                + "\nset handle to open for access targetFile with write permission"
                + "\ntry\nset eof handle to 0\nwrite imageData to handle\nclose access handle"
                + "\non error errMsg\ntry\nclose access handle\nend try\nerror errMsg\nend try";
            var result = await ProcessRunner.RunAsync("/usr/bin/osascript", new[] { "-" }, script);
            if (result.Code != 0 || !File.Exists(path))
                throw new IOException("Copy an image first (Control + Shift + Command + 4 for a screenshot).");
            if (new FileInfo(path).Length > maxBytes) throw new IOException("Image exceeds 20 MB.");
            return new ImageAttachment("Screenshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png",
                await File.ReadAllBytesAsync(path));
        }
        finally { Directory.Delete(folder, true); }
    }
}
