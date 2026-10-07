using Avalonia.Media.Imaging;
using SignalScheduler.Models;
using SignalScheduler.Presentation;

namespace SignalScheduler.ViewModels;

public sealed class AttachmentViewModel : IDisposable
{
    public ImageAttachment Attachment { get; }
    public string Name => Attachment.Name;
    public Bitmap? Preview { get; }
    public RelayCommand RemoveCommand { get; }

    public AttachmentViewModel(ImageAttachment attachment, Action<AttachmentViewModel> remove)
    {
        Attachment = attachment;
        RemoveCommand = new(() => remove(this));
        try
        {
            using var stream = new MemoryStream(attachment.Data);
            Preview = new Bitmap(stream);
        }
        catch { /* Unsupported image formats still remain valid file attachments. */ }
    }

    public void Dispose() => Preview?.Dispose();
}
