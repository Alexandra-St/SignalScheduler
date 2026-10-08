using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SignalScheduler.Infrastructure;
using SignalScheduler.Integrations.Signal;
using SignalScheduler.Models;
using SignalScheduler.Persistence;
using SignalScheduler.Security;
using SignalScheduler.Services;
using SignalScheduler.Presentation;

namespace SignalScheduler.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const int MaxBytes = 20 * 1024 * 1024;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly ObservableCollection<AttachmentViewModel> attachments = new();
    private EncryptedQueueStore? store;
    private MessageDispatcher? dispatcher;
    private string recipient = "", body = "";
    private string when = DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm");
    private string status = "Opening encrypted queue…";
    private string sendTimeError = "", recipientError = "";
    private bool canSchedule;
    public SignalCliConfiguration Signal { get; } = new();
    public string Recipient
    {
        get => recipient;
        set { if (Set(ref recipient, value ?? "")) ValidateRecipient(out _); }
    }
    public string RecipientError
    {
        get => recipientError;
        private set
        {
            if (!Set(ref recipientError, value)) return;
            Notify(nameof(HasRecipientError));
            NotifyScheduling();
        }
    }
    public bool HasRecipientError => RecipientError.Length > 0;
    public string Body { get => body; set => Set(ref body, value); }
    public string When
    {
        get => when;
        set { if (Set(ref when, value ?? "")) ValidateSendTime(out _); }
    }
    public string SendTimeError
    {
        get => sendTimeError;
        private set
        {
            if (!Set(ref sendTimeError, value)) return;
            Notify(nameof(HasSendTimeError));
            NotifyScheduling();
        }
    }
    public bool HasSendTimeError => SendTimeError.Length > 0;
    public string Status { get => status; private set => Set(ref status, value); }
    public bool CanSchedule => canSchedule && !IsBusy && Signal.HasSelectedAccount && !HasSendTimeError && !HasRecipientError;
    public bool IsBusy => dispatcher?.IsBusy ?? false;
    public IReadOnlyList<MessageViewModel> Messages { get; private set; } = Array.Empty<MessageViewModel>();
    public string QueueSummary => Messages.Count == 0 ? "No messages yet. Schedule your first message above."
        : $"{Messages.Count} messages · newest scheduled time first";
    public ReadOnlyObservableCollection<AttachmentViewModel> Attachments { get; }
    public RelayCommand ScheduleCommand { get; }
    public AsyncCommand PasteImageCommand { get; }

    public MainWindowViewModel()
    {
        Attachments = new(attachments);
        ScheduleCommand = new(Schedule, () => CanSchedule);
        PasteImageCommand = new(PasteImageAsync);
        Signal.PropertyChanged += (_, _) => NotifyScheduling();
        ValidateSendTime(out _);
        ValidateRecipient(out _);
        timer.Tick += async (_, _) =>
        {
            ValidateSendTime(out _);
            if (dispatcher != null && Signal.ExecutableReady && !Signal.IsWorking) await dispatcher.DispatchDueAsync();
        };
    }

    public async Task OpenAsync()
    {
        try
        {
            if (!OperatingSystem.IsMacOS())
                throw new PlatformNotSupportedException("This version requires macOS Keychain.");
            store = new EncryptedQueueStore(MacApplicationPaths.DataDirectory,
                await MacKeychain.GetOrCreateKeyAsync());
            MacApplicationPaths.DeleteTemporaryLeftovers();
            dispatcher = new MessageDispatcher(store, new ConfiguredSignalSender(Signal.ResolveExecutableAsync), () => Signal.Cli);
            dispatcher.SendingStarted += () =>
            {
                canSchedule = false;
                Signal.SetSending(true);
                NotifyScheduling();
                foreach (var message in Messages) message.NotifyCommandsChanged();
            };
            dispatcher.QueueChanged += UpdateMessages;
            dispatcher.QueueWriteFailed += () => QueueFailure("Queue write failed. Reopen the app.");
            dispatcher.SendingCompleted += outcome =>
            {
                Status = outcome switch
                {
                    DispatchOutcome.Accepted => "Message sent.",
                    DispatchOutcome.Uncertain => "Send failed. Check Signal before scheduling a copy.",
                    _ => "Sending or saving failed. Check Signal before rescheduling; reopen the app."
                };
                if (dispatcher.IsFaulted) timer.Stop();
                canSchedule = !dispatcher.IsFaulted;
                Signal.SetSending(false);
                NotifyScheduling();
            };
            canSchedule = true;
            NotifyScheduling();
            Status = "Ready to schedule messages.";
            UpdateMessages();
            timer.Start();
            await Signal.InitializeAsync();
        }
        catch (Exception exception) { Status = "Cannot open queue: " + exception.Message; }
    }

    public bool TryClose()
    {
        if (IsBusy)
        {
            Status = "Wait for the current send to finish before quitting.";
            return false;
        }
        dispatcher?.Close();
        Signal.Close();
        timer.Stop();
        ClearAttachments();
        store?.Dispose();
        return true;
    }

    private bool ValidateRecipient(out SignalRecipient? target)
    {
        var valid = SignalRecipient.TryParse(Recipient, out target);
        RecipientError = valid ? "" : SignalRecipient.Error;
        return valid;
    }

    private bool ValidateSendTime(out DateTimeOffset due)
    {
        var valid = SendTimeValidation.TryGetDue(When, DateTimeOffset.Now, TimeZoneInfo.Local, out due, out var error);
        SendTimeError = error;
        return valid;
    }

    public void Schedule()
    {
        // Recheck at activation: a time can become past after the last timer tick.
        var recipientValid = ValidateRecipient(out var target);
        var timeValid = ValidateSendTime(out var due);
        if (!recipientValid || !timeValid) return;
        if (store == null || IsBusy || !canSchedule) return;
        if (Signal.IsWorking)
        { Status = "Wait for account detection to finish."; return; }
        if (!File.Exists(Signal.Cli) || !Signal.HasSelectedAccount)
        { Status = "Set executable path and linked account before scheduling."; return; }
        if (string.IsNullOrWhiteSpace(Body) && attachments.Count == 0)
        { Status = "Add text or a photo."; return; }
        try
        {
            store.Add(new ScheduledMessage(Guid.NewGuid(), target!.Value, Body!, due,
                MessageStatus.Pending, Signal.Account.Trim(), Signal.Cli, attachments.Select(item => item.Attachment).ToList()));
            Body = "";
            ClearAttachments();
            UpdateMessages();
            Status = "Scheduled.";
        }
        catch { QueueFailure("Queue write failed. Reopen the app before continuing."); }
    }

    public void Cancel(ScheduledMessage message) => Mutate(() => store!.ChangeStatus(message.Id, MessageStatus.Cancelled));

    public void Delete(ScheduledMessage message) => Mutate(() => store!.Remove(message.Id));

    public void CopyToComposer(ScheduledMessage message)
    {
        Recipient = message.Recipient;
        Body = message.Text;
        ClearAttachments();
        foreach (var attachment in message.Attachments ?? new())
            attachments.Add(new AttachmentViewModel(attachment, RemoveAttachment));
        Signal.Account = Signal.LinkedAccounts.Contains(message.Account, StringComparer.Ordinal) ? message.Account : "";
        When = DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm");
    }

    public void RemoveAttachment(AttachmentViewModel attachment)
    {
        if (attachments.Remove(attachment)) attachment.Dispose();
    }

    private void AddAttachment(string name, byte[] data)
    {
        if (attachments.Count >= 8 || data.Length == 0 || attachments.Sum(item => (long)item.Attachment.Data.Length) + data.Length > MaxBytes)
            throw new IOException("Limit: 8 images, 20 MB total per message.");
        attachments.Add(new AttachmentViewModel(new ImageAttachment(Path.GetFileName(name), data), RemoveAttachment));
        Status = "Image added.";
    }

    public async Task AddImagesAsync(IReadOnlyList<IStorageFile> files)
    {
        try
        {
            foreach (var file in files)
            {
                await using var input = await file.OpenReadAsync();
                using var output = new MemoryStream();
                var buffer = new byte[81920];
                int count;
                while ((count = await input.ReadAsync(buffer)) > 0)
                {
                    if (output.Length + count > MaxBytes) throw new IOException("Image exceeds 20 MB.");
                    output.Write(buffer, 0, count);
                }
                AddAttachment(file.Name, output.ToArray());
            }
        }
        catch (Exception exception) { ReportImageError(exception); }
    }

    public void ReportImageError(Exception exception) => Status = "Could not add image: " + exception.Message;

    public async Task PasteImageAsync()
    {
        try
        {
            var attachment = await MacClipboardImageReader.ReadAsync(MaxBytes);
            AddAttachment(attachment.Name, attachment.Data);
        }
        catch (Exception exception) { Status = "Could not paste image: " + exception.Message; }
    }

    private void Mutate(Action action)
    {
        try { action(); UpdateMessages(); }
        catch { QueueFailure("Queue write failed. Reopen the app."); }
    }

    private void QueueFailure(string message)
    {
        timer.Stop();
        canSchedule = false;
        NotifyScheduling();
        Status = message;
    }

    private void NotifyScheduling()
    {
        Notify(nameof(CanSchedule));
        ScheduleCommand.NotifyCanExecuteChanged();
    }

    private void UpdateMessages()
    {
        Messages = store?.Items.OrderByDescending(message => message.Due)
            .Select(message => new MessageViewModel(message, this)).ToList()
            ?? (IReadOnlyList<MessageViewModel>)Array.Empty<MessageViewModel>();
        Notify(nameof(Messages));
        Notify(nameof(QueueSummary));
    }

    private void ClearAttachments()
    {
        foreach (var attachment in attachments) attachment.Dispose();
        attachments.Clear();
    }
}
