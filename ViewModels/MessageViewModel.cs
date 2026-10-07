using SignalScheduler.Models;
using SignalScheduler.Presentation;

namespace SignalScheduler.ViewModels;

public sealed class MessageViewModel
{
    public ScheduledMessage Message { get; }
    public string Recipient => "To " + Message.Recipient;
    public string SendAt => $"Send at {Message.Due.LocalDateTime:yyyy-MM-dd HH:mm} · local time";
    public string Status => "Status: " + MessageStatusLabels.Format(Message.State);
    public string? StatusHint => MessageStatusLabels.Hint(Message.State);
    public bool HasStatusHint => StatusHint != null;
    public string Text => Message.Text;
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
    public IReadOnlyList<ImageAttachment> Attachments => Message.Attachments ?? (IReadOnlyList<ImageAttachment>)Array.Empty<ImageAttachment>();
    public bool CanCancel => Message.State == MessageStatus.Pending;
    public bool CanReuse => Message.State != MessageStatus.Sending;
    public bool CanDelete => CanReuse && Message.State != MessageStatus.Pending;
    public RelayCommand CancelCommand { get; }
    public RelayCommand ReuseCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public MessageViewModel(ScheduledMessage message, MainWindowViewModel owner)
    {
        Message = message;
        CancelCommand = new(() => owner.Cancel(message), () => CanCancel && !owner.IsBusy);
        ReuseCommand = new(() => owner.CopyToComposer(message), () => CanReuse && !owner.IsBusy);
        DeleteCommand = new(() => owner.Delete(message), () => CanDelete && !owner.IsBusy);
    }

    public void NotifyCommandsChanged()
    {
        CancelCommand.NotifyCanExecuteChanged();
        ReuseCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }
}
