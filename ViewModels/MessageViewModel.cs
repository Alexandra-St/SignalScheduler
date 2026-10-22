using SignalScheduler.Models;
using SignalScheduler.Presentation;

namespace SignalScheduler.ViewModels;

public sealed class MessageViewModel : ObservableObject, IDisposable
{
    private IReadOnlyList<AttachmentViewModel>? previews;
    private readonly SignalCliConfiguration signal;
    private bool verificationPanelVisible, verificationInProgress;
    private string safetyNumber = "", verificationTrustStatus = "", verificationMessage = "";
    private string confirmedSafetyNumber = "";
    private bool canScheduleAgain;
    public ScheduledMessage Message { get; }
    public string RecipientIdentifier => Message.Recipient;
    public string ScheduledTime => Message.Due.LocalDateTime.ToString("HH:mm");
    public string ScheduledDate => Message.Due.LocalDateTime.ToString("d MMM");
    public string BadgeLabel => Message.State switch
    {
        MessageStatus.Cancelled => "Cancelled",
        MessageStatus.UnknownOrFailed => "Unknown / Failed",
        MessageStatus.RequiresVerification => "Requires verification",
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
        MessageStatus.RequiresVerification => "#70502D",
        _ => "#753E45"
    });
    public bool HasActions => CanCancel || CanReuse || CanReschedule || CanDelete;
    public IReadOnlyList<AttachmentViewModel> PreviewAttachments => previews ??= Attachments.Take(3)
        .Select(attachment => new AttachmentViewModel(attachment, _ => { })).ToArray();
    public bool HasMoreAttachments => Attachments.Count > 3;
    public string MoreAttachments => $"+{Attachments.Count - 3}";
    public void Dispose()
    {
        ClearVerificationData();
        if (previews != null) foreach (var preview in previews) preview.Dispose();
    }
    public string Recipient => "To " + Message.Recipient;
    public string SendAt => $"Send at {Message.Due.LocalDateTime:yyyy-MM-dd HH:mm} · local time";
    public string Status => "Status: " + MessageStatusLabels.Format(Message.State);
    public string? StatusHint => Message.State is MessageStatus.Unknown or MessageStatus.UnknownOrFailed
        ? "The result is uncertain. Check Signal before scheduling again to avoid duplicate messages."
        : MessageStatusLabels.Hint(Message.State);
    public bool HasStatusHint => StatusHint != null;
    public bool CanVerifyIdentity => Message.State == MessageStatus.RequiresVerification;
    public bool CanScheduleAgain => canScheduleAgain && CanVerifyIdentity && !verificationInProgress;
    public bool VerificationPanelVisible => verificationPanelVisible;
    public bool VerificationInProgress => verificationInProgress;
    public string SafetyNumber => safetyNumber;
    public string VerificationTrustStatus
    {
        get => verificationTrustStatus;
        private set
        {
            if (!Set(ref verificationTrustStatus, value)) return;
            Notify(nameof(HasTrustStatus));
            Notify(nameof(CanConfirmIdentity));
            Notify(nameof(CanCheckIdentityAgain));
            ConfirmIdentityCommand?.NotifyCanExecuteChanged();
        }
    }
    public string VerificationMessage { get => verificationMessage; private set => Set(ref verificationMessage, value); }
    public bool HasSafetyNumber => safetyNumber.Length > 0;
    public bool HasTrustStatus => verificationTrustStatus.Length > 0;
    public bool CanCheckIdentityAgain => confirmedSafetyNumber.Length > 0 && HasTrustStatus;
    public bool CanConfirmIdentity => HasSafetyNumber && verificationTrustStatus == "UNTRUSTED" && !verificationInProgress;
    public AsyncCommand VerifyIdentityCommand { get; }
    public AsyncCommand ConfirmIdentityCommand { get; }
    public AsyncCommand CheckIdentityAgainCommand { get; }
    public RelayCommand CloseVerificationCommand { get; }
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
        : this(message, owner, owner.Signal) { }

    internal MessageViewModel(ScheduledMessage message, MainWindowViewModel owner, SignalCliConfiguration signal)
    {
        Message = message;
        this.signal = signal;
        RescheduleCommand = new(() => owner.RequestReschedule(message), () => CanReschedule && !owner.IsBusy);
        EditCommand = new(() => owner.RequestTextEdit(message), () => CanEdit && !owner.IsBusy);
        CancelCommand = new(() => owner.Cancel(message), () => CanCancel && !owner.IsBusy);
        ReuseCommand = new(() => owner.CopyToComposer(message), () => CanReuse && !owner.IsBusy);
        DeleteCommand = new(() => owner.RequestDeletion(message), () => CanDelete && !owner.IsBusy);
        VerifyIdentityCommand = new(LoadIdentityAsync, () => CanVerifyIdentity && !verificationInProgress);
        ConfirmIdentityCommand = new(ConfirmIdentityAsync, () => CanConfirmIdentity);
        CheckIdentityAgainCommand = new(CheckIdentityAgainAsync, () => CanVerifyIdentity && !verificationInProgress && confirmedSafetyNumber.Length > 0);
        CloseVerificationCommand = new(HideVerificationPanel);
    }

    private async Task LoadIdentityAsync()
    {
        canScheduleAgain = false;
        Notify(nameof(CanScheduleAgain));
        CheckIdentityAgainCommand.NotifyCanExecuteChanged();
        verificationPanelVisible = true;
        Notify(nameof(VerificationPanelVisible));
        SetVerificationInProgress(true);
        SetSafetyNumber("");
        VerificationTrustStatus = "";
        VerificationMessage = "Reading the recipient's current identity…";
        try
        {
            var identity = await signal.ReadIdentityForVerificationAsync(Message);
            VerificationTrustStatus = identity.TrustStatus;
            if (identity.TrustStatus == "UNTRUSTED")
            {
                confirmedSafetyNumber = "";
                canScheduleAgain = false;
                SetSafetyNumber(identity.SafetyNumber);
                VerificationMessage = "Compare this safety number with the recipient through a separate trusted channel. If it does not match, do not confirm.";
            }
            else if (identity.TrustStatus == "TRUSTED_VERIFIED" && !string.IsNullOrWhiteSpace(identity.SafetyNumber))
            {
                confirmedSafetyNumber = identity.SafetyNumber;
                canScheduleAgain = true;
                Notify(nameof(CanScheduleAgain));
                Notify(nameof(CanCheckIdentityAgain));
                VerificationMessage = "This identity is already verified. You can choose a new time to schedule this message.";
            }
            else if (identity.TrustStatus == "TRUSTED_UNVERIFIED")
            {
                confirmedSafetyNumber = "";
                canScheduleAgain = false;
                Notify(nameof(CanScheduleAgain));
                VerificationMessage = "This identity is already trusted. No trust change was made.";
            }
            else
            {
                VerificationTrustStatus = "";
                VerificationMessage = "Signal returned an unsupported identity status. No changes were made.";
            }
        }
        catch
        {
            VerificationTrustStatus = "";
            VerificationMessage = "Could not read the recipient's identity. No changes were made.";
        }
        finally { SetVerificationInProgress(false); }
    }

    private async Task ConfirmIdentityAsync()
    {
        if (!CanConfirmIdentity) return;
        SetVerificationInProgress(true);
        VerificationMessage = "Verifying the confirmed identity…";
        var confirmedSafetyNumber = safetyNumber;
        try
        {
            if (await signal.TrustVerifiedIdentityForMessageAsync(Message, confirmedSafetyNumber))
            {
                this.confirmedSafetyNumber = confirmedSafetyNumber;
                Notify(nameof(CanCheckIdentityAgain));
                VerificationTrustStatus = "TRUSTED_VERIFIED";
                VerificationMessage = "Identity verified. No message was sent. You can choose a new time to schedule it.";
                canScheduleAgain = true;
                Notify(nameof(CanScheduleAgain));
                CheckIdentityAgainCommand.NotifyCanExecuteChanged();
            }
            else
            {
                VerificationTrustStatus = "";
                VerificationMessage = "The identity changed or no longer matches. Review the current safety number again before confirming.";
            }
        }
        catch
        {
            VerificationTrustStatus = "";
            VerificationMessage = "Signal could not verify this identity. No message was sent.";
        }
        finally
        {
            SetSafetyNumber("");
            SetVerificationInProgress(false);
        }
    }

    private async Task CheckIdentityAgainAsync()
    {
        if (confirmedSafetyNumber.Length == 0) return;
        await RecheckIdentityForScheduleAsync();
    }

    public async Task<bool> RecheckIdentityForScheduleAsync()
    {
        if (!CanVerifyIdentity || confirmedSafetyNumber.Length == 0 || verificationInProgress) return false;
        verificationPanelVisible = true;
        Notify(nameof(VerificationPanelVisible));
        SetVerificationInProgress(true);
        VerificationMessage = "Checking the recipient's identity again…";
        try
        {
            var verified = await signal.IsVerifiedIdentityForMessageAsync(Message, confirmedSafetyNumber);
            canScheduleAgain = verified;
            VerificationTrustStatus = verified ? "TRUSTED_VERIFIED" : "";
            VerificationMessage = verified
                ? "Identity is still verified. You can choose a new time to schedule this message."
                : "The identity is no longer verified. Review and verify it again before scheduling.";
            if (!verified) await RefreshIdentityAfterFailedRecheckAsync();
            Notify(nameof(CanScheduleAgain));
            return verified;
        }
        catch
        {
            canScheduleAgain = false;
            VerificationTrustStatus = "";
            VerificationMessage = "Could not confirm that this identity is still verified. Check it again before scheduling.";
            Notify(nameof(CanScheduleAgain));
            return false;
        }
        finally { SetVerificationInProgress(false); }
    }

    private async Task RefreshIdentityAfterFailedRecheckAsync()
    {
        try
        {
            var current = await signal.ReadIdentityForVerificationAsync(Message);
            VerificationTrustStatus = current.TrustStatus;
            if (current.TrustStatus == "UNTRUSTED")
            {
                SetSafetyNumber(current.SafetyNumber);
                VerificationMessage = "The identity is no longer verified. Compare this safety number with the recipient again before confirming.";
            }
        }
        catch { }
    }

    public void ClearVerificationData()
    {
        verificationPanelVisible = false;
        canScheduleAgain = false;
        confirmedSafetyNumber = "";
        Notify(nameof(VerificationPanelVisible));
        Notify(nameof(CanScheduleAgain));
        SetSafetyNumber("");
        VerificationTrustStatus = "";
        VerificationMessage = "";
    }

    private void HideVerificationPanel()
    {
        verificationPanelVisible = false;
        Notify(nameof(VerificationPanelVisible));
        SetSafetyNumber("");
        VerificationTrustStatus = "";
        VerificationMessage = "";
    }

    private void SetSafetyNumber(string value)
    {
        if (!Set(ref safetyNumber, value)) return;
        Notify(nameof(SafetyNumber));
        Notify(nameof(HasSafetyNumber));
        Notify(nameof(CanConfirmIdentity));
        Notify(nameof(CanCheckIdentityAgain));
        ConfirmIdentityCommand?.NotifyCanExecuteChanged();
        CheckIdentityAgainCommand?.NotifyCanExecuteChanged();
    }

    private void SetVerificationInProgress(bool value)
    {
        if (!Set(ref verificationInProgress, value)) return;
        Notify(nameof(VerificationInProgress));
        Notify(nameof(CanConfirmIdentity));
        Notify(nameof(CanScheduleAgain));
        VerifyIdentityCommand?.NotifyCanExecuteChanged();
        ConfirmIdentityCommand?.NotifyCanExecuteChanged();
    }

    public void NotifyCommandsChanged()
    {
        RescheduleCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ReuseCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        CheckIdentityAgainCommand.NotifyCanExecuteChanged();
    }
}
