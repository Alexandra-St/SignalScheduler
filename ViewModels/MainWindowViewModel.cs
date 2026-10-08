using System.Globalization;
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
    private bool canSchedule, recipientInteracted;
    private DateTime? selectedDate;
    private TimeSpan? selectedTime;
    private string dateInput = "", timeInput = "";
    public SignalCliConfiguration Signal { get; } = new();
    public string Recipient
    {
        get => recipient;
        set
        {
            if (!Set(ref recipient, value ?? "")) return;
            recipientInteracted = true;
            ValidateRecipient(out _);
            NotifyScheduling();
        }
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
    private string imageError = "";
    public string ImageError
    {
        get => imageError;
        private set { if (Set(ref imageError, value)) Notify(nameof(HasImageError)); }
    }
    public bool HasImageError => ImageError.Length > 0;
    public bool HasRecipientError => RecipientError.Length > 0;
    public string Body { get => body; set => Set(ref body, value); }
    public string When
    {
        get => when;
        set
        {
            if (!Set(ref when, value ?? "")) return;
            SyncPickers();
            ValidateSendTime(out _);
        }
    }
    public DateTime? SelectedDate
    {
        get => selectedDate;
        set { if (Set(ref selectedDate, value)) UpdatePickerTime(); }
    }
    public DateTime MinimumSelectableDate => DateTime.Today;
    public TimeSpan? SelectedTime
    {
        get => selectedTime;
        set
        {
            if (!Set(ref selectedTime, value)) return;
            Notify(nameof(SelectedTimeText));
            UpdatePickerTime();
        }
    }

    public string DateInput
    {
        get => dateInput;
        set
        {
            if (!Set(ref dateInput, value ?? "")) return;
            selectedDate = DateTime.TryParseExact(dateInput, new[] { "d.M.yyyy", "dd.MM.yyyy" }, CultureInfo.GetCultureInfo("en-GB"),
                DateTimeStyles.None, out var date) ? date.Date : null;
            Notify(nameof(SelectedDate));
            UpdatePickerTime();
        }
    }
    public string TimeInput
    {
        get => timeInput;
        set
        {
            if (!Set(ref timeInput, value ?? "")) return;
            selectedTime = DateTime.TryParseExact(timeInput, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time) ? time.TimeOfDay : null;
            Notify(nameof(SelectedTime));
            UpdatePickerTime();
        }
    }
    public bool HasDateInputError => HasSendTimeError && (!SelectedDate.HasValue || SelectedDate.Value.Date < DateTime.Today);
    public bool HasTimeInputError => HasSendTimeError && !HasDateInputError;
    public void NormalizeDateInput()
    {
        if (SelectedDate.HasValue) { dateInput = SelectedDate.Value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("en-GB")); Notify(nameof(DateInput)); }
    }
    public void NormalizeTimeInput()
    {
        if (SelectedTime.HasValue) { timeInput = SelectedTime.Value.ToString(@"hh\:mm"); Notify(nameof(TimeInput)); }
    }

    public string SelectedTimeText => SelectedTime?.ToString(@"hh\:mm", CultureInfo.InvariantCulture) ?? "Choose time";

    private void SyncPickers()
    {
        var valid = DateTime.TryParseExact(When, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var local);
        selectedDate = valid ? local.Date : null;
        selectedTime = valid ? local.TimeOfDay : null;
        Notify(nameof(SelectedDate));
        Notify(nameof(SelectedTime));
        Notify(nameof(SelectedTimeText));
        dateInput = selectedDate?.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("en-GB")) ?? "";
        timeInput = selectedTime?.ToString(@"hh\:mm") ?? "";
        Notify(nameof(DateInput));
        Notify(nameof(TimeInput));
    }

    private void UpdatePickerTime()
    {
        // Pickers describe wall-clock time. SendTimeValidation resolves the local offset and DST.
        var text = SelectedDate.HasValue && SelectedTime.HasValue
            ? SelectedDate.Value.Date.Add(SelectedTime.Value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "";
        Set(ref when, text, nameof(When));
        ValidateSendTime(out _);
    }

    public string SendTimeError
    {
        get => sendTimeError;
        private set
        {
            if (!Set(ref sendTimeError, value)) return;
            Notify(nameof(HasSendTimeError));
            Notify(nameof(HasDateInputError));
            Notify(nameof(HasTimeInputError));
            NotifyScheduling();
        }
    }
    public bool HasSendTimeError => SendTimeError.Length > 0;
    public string Status { get => status; private set => Set(ref status, value); }
    public bool CanSchedule => canSchedule && !IsBusy && Signal.HasSelectedAccount && !HasSendTimeError && SignalRecipient.TryParse(Recipient, out _);
    public bool IsBusy => dispatcher?.IsBusy ?? false;
    public IReadOnlyList<MessageViewModel> Messages { get; private set; } = Array.Empty<MessageViewModel>();
    private HistoryFilterOption historyFilter;
    private DateTime presentationDate = DateTime.Today;
    public bool IsReviewingNewMissed => HistoryFilter.Kind == HistoryFilterKind.NewMissed;
    public string ShowAllMissedLabel => $"Show all missed ({Messages.Count(item => item.Message.State == MessageStatus.Missed)})";
    public ObservableCollection<HistoryFilterOption> HistoryFilters { get; } = new(HistoryFilterOption.Create(0, false));
    public bool ShowSavedHistoryNotice => !Signal.HasSelectedAccount && Messages.Count > 0;
    public HistoryFilterOption HistoryFilter
    {
        get => historyFilter;
        set
        {
            if (value == null || !Set(ref historyFilter, value)) return;
            Notify(nameof(IsReviewingNewMissed));
            RefreshMessageGroups();
        }
    }
    private void SelectHistoryFilter(HistoryFilterKind kind)
        => HistoryFilter = HistoryFilters.Single(option => option.Kind == kind);
    public IReadOnlyList<MessageGroup> UpcomingGroups { get; private set; } = Array.Empty<MessageGroup>();
    public IReadOnlyList<MessageViewModel> HistoryMessages { get; private set; } = Array.Empty<MessageViewModel>();
    public bool NoUpcoming => UpcomingGroups.Count == 0;
    public bool NoHistory => HistoryMessages.Count == 0;
    public string UpcomingHeader => $"Upcoming ({Messages.Count(m => m.Message.State is MessageStatus.Pending or MessageStatus.Sending)})";
    public string HistoryHeader => $"History ({Messages.Count(m => m.Message.State is not (MessageStatus.Pending or MessageStatus.Sending))})";
    private void RefreshMessageGroups()
    {
        presentationDate = DateTime.Today;
        UpcomingGroups = MessageQueuePresentation.Upcoming(Messages, presentationDate);
        var newMissed = HistoryFilters.SingleOrDefault(option => option.Kind == HistoryFilterKind.NewMissed);
        if (startupMissedCount > 0)
        {
            if (newMissed == null)
                HistoryFilters.Insert(3, new(HistoryFilterKind.NewMissed, $"Missed ({startupMissedCount})"));
            else newMissed.UpdateCount(startupMissedCount);
        }
        else if (newMissed != null)
        {
            if (IsReviewingNewMissed) SelectHistoryFilter(HistoryFilterKind.All);
            HistoryFilters.Remove(newMissed);
        }
        HistoryMessages = IsReviewingNewMissed
            ? MessageQueuePresentation.History(Messages, HistoryFilterKind.NewMissed).Where(item => startupMissedIds.Contains(item.Message.Id)).ToArray()
            : MessageQueuePresentation.History(Messages, HistoryFilter.Kind);
        Notify(nameof(ShowAllMissedLabel));
        Notify(nameof(IsReviewingNewMissed));
        Notify(nameof(UpcomingGroups));
        Notify(nameof(HistoryMessages));
        Notify(nameof(NoUpcoming));
        Notify(nameof(NoHistory));
        Notify(nameof(UpcomingHeader));
        Notify(nameof(HistoryHeader));
    }

    public string QueueSummary => Messages.Count == 0 ? "No messages yet. Schedule your first message on the left."
        : $"{Messages.Count} messages · newest scheduled time first";
    public ReadOnlyObservableCollection<AttachmentViewModel> Attachments { get; }
    public RelayCommand ScheduleCommand { get; }
    public AsyncCommand PasteImageCommand { get; }

    public MainWindowViewModel()
    {
        historyFilter = HistoryFilters[0];
        Attachments = new(attachments);
        ScheduleCommand = new(Schedule, () => CanSchedule);
        PasteImageCommand = new(PasteImageAsync);
        DismissMissedCommand = new(() => { startupNoticeDismissed = true; Notify(nameof(HasStartupMissed)); });
        ReviewMissedCommand = new(() => { SelectHistoryFilter(HistoryFilterKind.NewMissed); SelectedMessagesTab = 1; }, () => startupMissedCount > 0);
        ShowAllMissedCommand = new(() => { SelectHistoryFilter(HistoryFilterKind.Missed); SelectedMessagesTab = 1; });
        Signal.PropertyChanged += (_, _) => { NotifyScheduling(); Notify(nameof(ShowSavedHistoryNotice)); };
        SyncPickers();
        ValidateSendTime(out _);
        ValidateRecipient(out _);
        timer.Tick += async (_, _) =>
        {
            ValidateSendTime(out _);
            if (presentationDate != DateTime.Today) RefreshMessageGroups();
            if (dispatcher != null && Signal.ExecutableReady && !Signal.IsWorking) await dispatcher.DispatchDueAsync();
        };
    }

    // Internal dependency seam: isolated queue/sender, without Keychain, account discovery or timers.
    internal MainWindowViewModel(EncryptedQueueStore queue, ISignalSender sender) : this()
    {
        ConfigureQueue(queue, sender);
        canSchedule = true;
        UpdateMessages();
    }

    private void ConfigureQueue(EncryptedQueueStore queue, ISignalSender sender, Func<string?>? currentExecutable = null)
    {
        store = queue;
        CaptureStartupOverdue();
        dispatcher = new MessageDispatcher(queue, sender, currentExecutable);
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
    }

    internal async Task ProcessStartupOverdueAsync()
    {
        while (startupOverdue.Count > 0)
        {
            // DispatchDueAsync processes the oldest Pending; startup-overdue items precede any sendable item.
            var oldest = store!.Items.Where(item => startupOverdue.Contains(item.Id)).OrderBy(item => item.Due).First();
            await dispatcher!.DispatchDueAsync();
            // Only the successful QueueChanged event acknowledges this specific persisted transition.
            // Store memory may already say Missed even when its disk write failed.
            if (startupOverdue.Contains(oldest.Id))
                throw new IOException("Could not save missed message status. Reopen the app.");
        }
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
            ConfigureQueue(store, new ConfiguredSignalSender(Signal.ResolveExecutableAsync), () => Signal.Cli);
            await ProcessStartupOverdueAsync();
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
        foreach (var message in Messages) message.Dispose();
        store?.Dispose();
        return true;
    }

    private bool ValidateRecipient(out SignalRecipient? target)
    {
        var valid = SignalRecipient.TryParse(Recipient, out target);
        RecipientError = valid || !recipientInteracted ? "" : SignalRecipient.Error;
        return valid;
    }

    private bool ValidateSendTime(out DateTimeOffset due)
    {
        var valid = SendTimeValidation.TryGetDue(When, DateTimeOffset.Now, TimeZoneInfo.Local, out due, out var error);
        if (!SelectedDate.HasValue) error = "Enter a valid date, for example 08.10.2026.";
        else if (!SelectedTime.HasValue) error = "Enter a valid time in HH:mm format.";
        SendTimeError = error;
        Notify(nameof(HasDateInputError));
        Notify(nameof(HasTimeInputError));
        return valid;
    }

    public void Schedule()
    {
        // Recheck at activation: a time can become past after the last timer tick.
        recipientInteracted = true;
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

    public void Cancel(ScheduledMessage message)
    {
        if (IsBusy || store?.Items.SingleOrDefault(item => item.Id == message.Id)?.State != MessageStatus.Pending) return;
        Mutate(() => store!.ChangeStatus(message.Id, MessageStatus.Cancelled));
    }

    public event Action<ScheduledMessage>? RescheduleRequested;
    public void RequestReschedule(ScheduledMessage message)
    {
        var current = store?.Items.SingleOrDefault(item => item.Id == message.Id);
        if (!IsBusy && current?.State == MessageStatus.Missed) RescheduleRequested?.Invoke(current);
    }
    public bool TryReschedule(Guid id, string date, string time, out string error)
    {
        error = "This missed message is no longer available.";
        var original = store?.Items.SingleOrDefault(item => item.Id == id);
        if (original?.State != MessageStatus.Missed) return false;
        if (!DateTime.TryParseExact(date, new[] { "d.M.yyyy", "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        { error = "Enter a valid date, for example 08.10.2026."; return false; }
        var canonical = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + time;
        if (!SendTimeValidation.TryGetDue(canonical, DateTimeOffset.Now, TimeZoneInfo.Local, out var due, out error)) return false;
        if (IsBusy || !canSchedule || Signal.IsWorking)
        { error = "Wait until Signal Scheduler is ready."; return false; }
        if (!File.Exists(Signal.Cli) || !Signal.LinkedAccounts.Contains(original.Account, StringComparer.Ordinal))
        { error = "Connect the original sending account in Settings before rescheduling."; return false; }
        try
        {
            store!.Add(original with { Id = Guid.NewGuid(), Due = due, State = MessageStatus.Pending, Cli = Signal.Cli });
            startupMissedIds.Remove(original.Id);
            UpdateMessages();
            SelectedMessagesTab = 0;
            Status = "Message rescheduled. The original remains in History.";
            error = "";
            return true;
        }
        catch { error = "Queue write failed. Reopen the app."; QueueFailure(error); return false; }
    }
    private HashSet<Guid> startupOverdue = new();
    private readonly HashSet<Guid> startupMissedIds = new();
    private bool startupNoticeDismissed;
    private int startupMissedCount;
    public bool HasStartupMissed => startupMissedCount > 0 && !startupNoticeDismissed;
    public RelayCommand DismissMissedCommand { get; }
    public string StartupMissedNotice => startupMissedCount == 1
        ? "1 scheduled message was missed while Signal Scheduler was inactive."
        : $"{startupMissedCount} scheduled messages were missed while Signal Scheduler was inactive.";
    private int selectedMessagesTab;
    public int SelectedMessagesTab { get => selectedMessagesTab; set => Set(ref selectedMessagesTab, value); }
    public RelayCommand ReviewMissedCommand { get; }
    public RelayCommand ShowAllMissedCommand { get; }
    private void CaptureStartupOverdue()
    {
        startupOverdue = store!.Items.Where(item => item.State == MessageStatus.Pending &&
            DateTimeOffset.UtcNow - item.Due > TimeSpan.FromMinutes(5)).Select(item => item.Id).ToHashSet();
    }

    public event Action<ScheduledMessage>? TextEditRequested;
    public void RequestTextEdit(ScheduledMessage message)
    {
        if (!IsBusy && store?.Items.Any(item => item.Id == message.Id && item.State == MessageStatus.Pending) == true)
            TextEditRequested?.Invoke(message);
    }

    public bool SavePendingText(Guid id, string text, out string error)
    {
        error = "This message can no longer be edited. It may already be sending.";
        if (IsBusy || !canSchedule || store == null) return false;
        var message = store.Items.SingleOrDefault(item => item.Id == id);
        if (message?.State != MessageStatus.Pending) return false;
        if (string.IsNullOrWhiteSpace(text) && (message.Attachments?.Count ?? 0) == 0)
        { error = "Add text or a photo."; return false; }
        try
        {
            if (!store.TryEditPendingText(id, text)) return false;
            UpdateMessages();
            Status = "Message text updated.";
            error = "";
            return true;
        }
        catch
        {
            error = "Could not save the message. Reopen the app before continuing.";
            QueueFailure(error);
            return false;
        }
    }

    public event Action<ScheduledMessage>? DeleteConfirmationRequested;
    public void RequestDeletion(ScheduledMessage message)
    {
        var current = store?.Items.SingleOrDefault(item => item.Id == message.Id);
        if (!IsBusy && current != null && current.State is not (MessageStatus.Pending or MessageStatus.Sending))
            DeleteConfirmationRequested?.Invoke(current);
    }

    public void Delete(ScheduledMessage message)
    {
        var current = store?.Items.SingleOrDefault(item => item.Id == message.Id);
        if (IsBusy || current == null || current.State is MessageStatus.Pending or MessageStatus.Sending) return;
        Mutate(() => store!.Remove(message.Id));
    }

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
        ImageError = "";
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

    public void ReportImageError(Exception exception) => ImageError = "Could not add image: " + exception.Message;

    public async Task PasteImageAsync()
    {
        try
        {
            var attachment = await MacClipboardImageReader.ReadAsync(MaxBytes);
            AddAttachment(attachment.Name, attachment.Data);
        }
        catch (Exception exception) { ImageError = exception.Message; }
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
        foreach (var id in startupOverdue.ToArray())
            if (store?.Items.Any(item => item.Id == id && item.State == MessageStatus.Missed) == true)
            { startupMissedIds.Add(id); startupOverdue.Remove(id); }
        startupMissedIds.RemoveWhere(id => store?.Items.Any(item => item.Id == id && item.State == MessageStatus.Missed) != true);
        startupMissedCount = startupMissedIds.Count;
        ReviewMissedCommand.NotifyCanExecuteChanged();
        Notify(nameof(HasStartupMissed));
        Notify(nameof(StartupMissedNotice));
        foreach (var message in Messages) message.Dispose();
        Messages = store?.Items.OrderByDescending(message => message.Due)
            .Select(message => new MessageViewModel(message, this)).ToList()
            ?? (IReadOnlyList<MessageViewModel>)Array.Empty<MessageViewModel>();
        Notify(nameof(Messages));
        Notify(nameof(ShowSavedHistoryNotice));
        Notify(nameof(QueueSummary));
        RefreshMessageGroups();
    }

    private void ClearAttachments()
    {
        ImageError = "";
        foreach (var attachment in attachments) attachment.Dispose();
        attachments.Clear();
    }
}
