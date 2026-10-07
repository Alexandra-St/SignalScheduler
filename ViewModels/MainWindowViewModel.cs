using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SignalScheduler.Infrastructure;
using SignalScheduler.Integrations.Signal;
using SignalScheduler.Models;
using SignalScheduler.Persistence;
using SignalScheduler.Security;
using SignalScheduler.Services;

namespace SignalScheduler.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const int MaxBytes = 20 * 1024 * 1024;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly List<ImageAttachment> attachments = new();
    private EncryptedQueueStore? store;
    private MessageDispatcher? dispatcher;
    private string cli = "";
    private string? customExecutable;
    private string executableStatus = "Looking for signal-cli…", executableVersion = "";
    private bool isResolvingExecutable;
    public string ExecutableStatus { get => executableStatus; private set => Set(ref executableStatus, value); }
    public string ExecutableVersion { get => executableVersion; private set => Set(ref executableVersion, value); }
    public bool AutomaticDetection => customExecutable == null;
    public bool CanConfigureExecutable => !IsBusy && !IsDiscoveringAccounts && !isResolvingExecutable;
    public bool ExecutableReady => !string.IsNullOrEmpty(Cli) && !isResolvingExecutable;

    private string account = "", recipient = "", body = "";
    private string when = DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm");
    private string status = "Opening encrypted queue…";
    private bool canSchedule;
    private bool isDiscoveringAccounts, closed;
    private IReadOnlyList<string> linkedAccounts = Array.Empty<string>();
    private string accountDiscoveryStatus = "";

    public string Cli
    {
        get => cli;
        set
        {
            if (cli == value) return;
            Set(ref cli, value);
            LinkedAccounts = Array.Empty<string>();
            Account = "";
            AccountDiscoveryStatus = "Executable path changed. Refresh accounts for the new path.";
        }
    }
    public string Account { get => account; set => Set(ref account, value); }
    public string Recipient { get => recipient; set => Set(ref recipient, value); }
    public string Body { get => body; set => Set(ref body, value); }
    public string When { get => when; set => Set(ref when, value); }
    public string Status { get => status; private set => Set(ref status, value); }
    public bool CanSchedule { get => canSchedule && ExecutableReady && !IsDiscoveringAccounts && LinkedAccounts.Contains(Account, StringComparer.Ordinal); private set => Set(ref canSchedule, value); }
    public bool IsBusy => dispatcher?.IsBusy ?? false;
    public bool IsDiscoveringAccounts { get => isDiscoveringAccounts; private set => Set(ref isDiscoveringAccounts, value); }
    public IReadOnlyList<string> LinkedAccounts { get => linkedAccounts; private set => Set(ref linkedAccounts, value); }
    public string AccountDiscoveryStatus { get => accountDiscoveryStatus; private set => Set(ref accountDiscoveryStatus, value); }
    public IEnumerable<ScheduledMessage> Messages => store == null
        ? Enumerable.Empty<ScheduledMessage>()
        : store.Items.OrderByDescending(message => message.Due);
    public IReadOnlyList<ImageAttachment> Attachments => attachments;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? QueueChanged;
    public event Action? AttachmentsChanged;

    public MainWindowViewModel()
    {
        timer.Tick += async (_, _) =>
        {
            if (dispatcher != null && ExecutableReady && !IsDiscoveringAccounts) await dispatcher.DispatchDueAsync();
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
            dispatcher = new MessageDispatcher(store, new ConfiguredSignalSender(ResolveCurrentExecutableAsync), () => Cli);
            dispatcher.SendingStarted += () =>
            {
                CanSchedule = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanConfigureExecutable)));
            };
            dispatcher.QueueChanged += () => QueueChanged?.Invoke();
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
                CanSchedule = !dispatcher.IsFaulted;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanConfigureExecutable)));
            };
            CanSchedule = true;
            Status = "Ready to schedule messages.";
            QueueChanged?.Invoke();
            timer.Start();
            try { customExecutable = SignalCliPreferences.Load(); }
            catch { ExecutableStatus = "Could not read Settings. Choose an executable or use automatic detection."; return; }
            await ConfigureExecutableAsync(customExecutable, persist: false);
        }
        catch (Exception exception) { Status = "Cannot open queue: " + exception.Message; }
    }

    public void ReportExecutableSelectionError() => ExecutableStatus = "Could not choose executable. Try again.";

    private Task<SignalExecutable?> ResolveCurrentExecutableAsync() => customExecutable == null
        ? SignalExecutable.DetectAsync(Environment.GetEnvironmentVariable("PATH"))
        : SignalExecutable.ValidateAsync(customExecutable);

    public async Task ConfigureExecutableAsync(string? chosenPath, bool persist = true)
    {
        if (!CanConfigureExecutable || closed) return;
        isResolvingExecutable = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanConfigureExecutable)));
        Cli = "";
        LinkedAccounts = Array.Empty<string>();
        Account = "";
        ExecutableVersion = "";
        ExecutableStatus = "Looking for signal-cli…";
        try
        {
            var executable = chosenPath == null
                ? await SignalExecutable.DetectAsync(Environment.GetEnvironmentVariable("PATH"))
                : await SignalExecutable.ValidateAsync(chosenPath);
            if (closed) return;
            if (chosenPath != null && executable == null)
            {
                AccountDiscoveryStatus = "";
                ExecutableStatus = "Invalid executable. Choose a working signal-cli or use automatic detection.";
                return;
            }
            if (persist) SignalCliPreferences.Save(chosenPath);
            customExecutable = chosenPath;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutomaticDetection)));
            Cli = executable?.Path ?? "";
            ExecutableVersion = executable?.Version ?? "";
            ExecutableStatus = executable == null
                ? "signal-cli not found. Install signal-cli or choose its location in Settings."
                : "signal-cli ready";
        }
        catch { ExecutableStatus = "Could not save Settings. Try again."; Cli = ""; }
        finally
        {
            isResolvingExecutable = false;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanConfigureExecutable)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSchedule)));
        }
        if (ExecutableReady) await RefreshAccountsAsync();
        else AccountDiscoveryStatus = "";
    }

    public async Task RefreshAccountsAsync()
    {
        if (closed || IsBusy || IsDiscoveringAccounts || !ExecutableReady) return;
        var executable = Cli;
        IsDiscoveringAccounts = true;
        AccountDiscoveryStatus = "Looking for linked accounts…";
        try
        {
            var accounts = await SignalAccountDiscovery.ListAsync(executable);
            if (closed || Cli != executable) return;
            LinkedAccounts = accounts;
            Account = accounts.Count == 1 ? accounts[0]
                : accounts.Contains(Account, StringComparer.Ordinal) ? Account : "";
            AccountDiscoveryStatus = accounts.Count switch
            {
                0 => "No local accounts found. Link signal-cli using the README instructions, then refresh.",
                1 => "Account ready",
                _ => "Multiple accounts found. Choose the account to send from."
            };
        }
        catch
        {
            if (!closed && Cli == executable)
            {
                LinkedAccounts = Array.Empty<string>();
                Account = "";
                AccountDiscoveryStatus = "Could not detect accounts. Check the executable path and refresh.";
            }
        }
        finally
        {
            IsDiscoveringAccounts = false;
            if (!closed && Cli != executable)
                AccountDiscoveryStatus = "Executable path changed. Refresh accounts for the new path.";
        }
    }

    public bool TryClose()
    {
        if (IsBusy)
        {
            Status = "Wait for the current send to finish before quitting.";
            return false;
        }
        dispatcher?.Close();
        closed = true;
        timer.Stop();
        store?.Dispose();
        return true;
    }

    public void Schedule()
    {
        if (store == null || IsBusy || !canSchedule) return;
        if (IsDiscoveringAccounts)
        { Status = "Wait for account detection to finish."; return; }
        if (!Regex.IsMatch(Recipient ?? "", @"^\+[1-9]\d{6,14}$"))
        { Status = "Enter recipient in international format."; return; }
        if (!File.Exists(Cli) || !LinkedAccounts.Contains(Account, StringComparer.Ordinal))
        { Status = "Set executable path and linked account before scheduling."; return; }
        if (string.IsNullOrWhiteSpace(Body) && attachments.Count == 0)
        { Status = "Add text or a photo."; return; }
        if (!DateTime.TryParseExact(When, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        { Status = "Use yyyy-MM-dd HH:mm."; return; }
        if (TimeZoneInfo.Local.IsInvalidTime(date) || TimeZoneInfo.Local.IsAmbiguousTime(date))
        { Status = "This time is ambiguous or skipped by daylight saving. Choose another time."; return; }
        var due = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
        if (due <= DateTimeOffset.Now) { Status = "Choose a future time."; return; }
        try
        {
            store.Items.Add(new ScheduledMessage(Guid.NewGuid(), Recipient!, Body!, due,
                MessageStatus.Pending, Account.Trim(), Cli, attachments.ToList()));
            store.Save();
            Body = "";
            attachments.Clear();
            AttachmentsChanged?.Invoke();
            QueueChanged?.Invoke();
            Status = "Scheduled.";
        }
        catch { QueueFailure("Queue write failed. Reopen the app before continuing."); }
    }

    public void Cancel(ScheduledMessage message) => Mutate(() => store!.Change(message.Id, MessageStatus.Cancelled));

    public void Delete(ScheduledMessage message) => Mutate(() =>
    {
        store!.Items.RemoveAll(item => item.Id == message.Id);
        store.Save();
    });

    public void CopyToComposer(ScheduledMessage message)
    {
        Recipient = message.Recipient;
        Body = message.Text;
        attachments.Clear();
        attachments.AddRange(message.Attachments ?? new());
        AttachmentsChanged?.Invoke();
        Account = LinkedAccounts.Contains(message.Account, StringComparer.Ordinal) ? message.Account : "";
        When = DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm");
    }

    public void RemoveAttachment(ImageAttachment attachment)
    {
        attachments.Remove(attachment);
        AttachmentsChanged?.Invoke();
    }

    private void AddAttachment(string name, byte[] data)
    {
        if (attachments.Count >= 8 || data.Length == 0 || attachments.Sum(item => (long)item.Data.Length) + data.Length > MaxBytes)
            throw new IOException("Limit: 8 images, 20 MB total per message.");
        attachments.Add(new ImageAttachment(Path.GetFileName(name), data));
        AttachmentsChanged?.Invoke();
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
        try { action(); QueueChanged?.Invoke(); }
        catch { QueueFailure("Queue write failed. Reopen the app."); }
    }

    private void QueueFailure(string message)
    {
        timer.Stop();
        CanSchedule = false;
        Status = message;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name is nameof(IsDiscoveringAccounts))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanConfigureExecutable)));
        if (name is nameof(Account) or nameof(LinkedAccounts) or nameof(IsDiscoveringAccounts) or nameof(Cli))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSchedule)));
    }
}
