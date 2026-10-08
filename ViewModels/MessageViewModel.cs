using SignalScheduler.Models;
using SignalScheduler.Presentation;

namespace SignalScheduler.ViewModels;

public sealed class MessageViewModel : IDisposable
{
    private IReadOnlyList<AttachmentViewModel>? previews;
    public ScheduledMessage Message { get; }
    public string RecipientIdentifier => Message.Recipient;
    public string ScheduledTime => Message.Due.LocalDateTime.ToString("HH:mm");
    public string ScheduledDate => Message.Due.LocalDateTime.ToString("d MMM");
    public string BadgeLabel => Message.State switch
    {
        MessageStatus.Cancelled => "Cancelled",
        MessageStatus.UnknownOrFailed => "Unknown / Failed",
        _ => MessageStatusLabels.Format(Message.State)
    };
    public Avalonia.Media.IBrush BadgeBackground => Avalonia.Media.Brush.Parse(Message.State switch
    {
        MessageStatus.Pending => "#665329",
        MessageStatus.Sending => "#294E74",
        MessageStatus.Sent => "#285C48",
        MessageStatus.Cancelled => "#404B58",
        MessageStatus.Missed => "#70502D",
        MessageStatus.Blocked => "#684C36",
        _ => "#753E45"
    });
    public bool HasActions => CanCancel || CanReuse || CanReschedule || CanDelete;
    public IReadOnlyList<AttachmentViewModel> PreviewAttachments => previews ??= Attachments.Take(3)
        .Select(attachment => new AttachmentViewModel(attachment, _ => { })).ToArray();
    public bool HasMoreAttachments => Attachments.Count > 3;
    public string MoreAttachments => $"+{Attachments.Count - 3}";
    public void Dispose()
    {
        if (previews != null) foreach (var preview in previews) preview.Dispose();
    }
    public string Recipient => "To " + Message.Recipient;
    public string SendAt => $"Send at {Message.Due.LocalDateTime:yyyy-MM-dd HH:mm} · local time";
    public string Status => "Status: " + MessageStatusLabels.Format(Message.State);
    public string? StatusHint => Message.State is MessageStatus.Unknown or MessageStatus.UnknownOrFailed
        ? "The result is uncertain. Check Signal before scheduling again to avoid duplicate messages."
        : MessageStatusLabels.Hint(Message.State);
    public bool HasStatusHint => StatusHint != null;
    public string Text => Message.Text;
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
    public IReadOnlyList<ImageAttachment> Attachments => Message.Attachments ?? (IReadOnlyList<ImageAttachment>)Array.Empty<ImageAttachment>();
    public bool CanCancel => Message.State == MessageStatus.Pending;
    public bool CanEdit => Message.State == MessageStatus.Pending;
    public RelayCommand EditCommand { get; }
    public bool CanReschedule => Message.State == MessageStatus.Missed;
    public bool ShowReuse => CanReuse && !CanReschedule;
    public RelayCommand RescheduleCommand { get; }
    public bool CanReuse => Message.State != MessageStatus.Sending;
    public bool CanDelete => CanReuse && Message.State != MessageStatus.Pending;
    public RelayCommand CancelCommand { get; }
    public RelayCommand ReuseCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public MessageViewModel(ScheduledMessage message, MainWindowViewModel owner)
    {
        Message = message;
        RescheduleCommand = new(() => owner.RequestReschedule(message), () => CanReschedule && !owner.IsBusy);
        EditCommand = new(() => owner.RequestTextEdit(message), () => CanEdit && !owner.IsBusy);
        CancelCommand = new(() => owner.Cancel(message), () => CanCancel && !owner.IsBusy);
        ReuseCommand = new(() => owner.CopyToComposer(message), () => CanReuse && !owner.IsBusy);
        DeleteCommand = new(() => owner.RequestDeletion(message), () => CanDelete && !owner.IsBusy);
    }

    public void NotifyCommandsChanged()
    {
        RescheduleCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ReuseCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }
}
