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
    private string cli = File.Exists("/opt/homebrew/bin/signal-cli")
        ? "/opt/homebrew/bin/signal-cli" : "/usr/local/bin/signal-cli";
    private string account = "", recipient = "", body = "";
    private string when = DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm");
    private string status = "Opening encrypted queue…";
    private bool canSchedule;

    public string Cli { get => cli; set => Set(ref cli, value); }
    public string Account { get => account; set => Set(ref account, value); }
    public string Recipient { get => recipient; set => Set(ref recipient, value); }
    public string Body { get => body; set => Set(ref body, value); }
    public string When { get => when; set => Set(ref when, value); }
    public string Status { get => status; private set => Set(ref status, value); }
    public bool CanSchedule { get => canSchedule; private set => Set(ref canSchedule, value); }
    public bool IsBusy => dispatcher?.IsBusy ?? false;
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
            if (dispatcher != null) await dispatcher.DispatchDueAsync();
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
            dispatcher = new MessageDispatcher(store, new SignalCliAdapter());
            dispatcher.SendingStarted += () => CanSchedule = false;
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
            };
            CanSchedule = true;
            Status = "Ready. Account linking instructions are in README.";
            QueueChanged?.Invoke();
            timer.Start();
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
        timer.Stop();
        store?.Dispose();
        return true;
    }

    public void Schedule()
    {
        if (store == null || IsBusy) return;
        if (!Regex.IsMatch(Recipient ?? "", @"^\+[1-9]\d{6,14}$"))
        { Status = "Enter recipient in international format."; return; }
        if (!File.Exists(Cli) || string.IsNullOrWhiteSpace(Account))
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
        Account = message.Account;
        Cli = message.Cli;
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
    }
}
