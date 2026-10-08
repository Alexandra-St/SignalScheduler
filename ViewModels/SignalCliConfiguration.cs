using SignalScheduler.Infrastructure;
using SignalScheduler.Integrations.Signal;
using SignalScheduler.Presentation;

namespace SignalScheduler.ViewModels;

public sealed class SignalCliConfiguration : ObservableObject
{
    private readonly Func<Task<SignalExecutable?>> detectExecutable;
    private readonly Action<string?> savePreference;
    private string? customExecutable;
    private string cli = "", version = "", account = "";
    private string executableStatus = "Looking for signal-cli…", accountDiscoveryStatus = "";
    private IReadOnlyList<string> linkedAccounts = Array.Empty<string>();
    private bool resolving, discovering, linking, locked, closed, accountsKnown;

    public string Cli { get => cli; private set => Set(ref cli, value); }
    public string ExecutableVersion { get => version; private set => Set(ref version, value); }
    public string ExecutableStatus { get => executableStatus; private set => Set(ref executableStatus, value); }
    public string AccountDiscoveryStatus { get => accountDiscoveryStatus; private set => Set(ref accountDiscoveryStatus, value); }
    public IReadOnlyList<string> LinkedAccounts { get => linkedAccounts; private set => Set(ref linkedAccounts, value); }
    public string Account
    {
        get => account;
        set { if (Set(ref account, value ?? "")) Notify(nameof(HasSelectedAccount)); }
    }
    public bool AutomaticDetection => customExecutable == null;
    public bool ExecutableReady => Cli.Length > 0 && !resolving;
    public bool IsWorking => resolving || discovering || linking;
    public bool CanConfigure => !closed && !locked && !IsWorking;
    public bool HasSelectedAccount => ExecutableReady && !IsWorking && LinkedAccounts.Contains(Account, StringComparer.Ordinal);
    public bool CanConnect => CanConfigure && ExecutableReady;
    public bool NeedsConnection => accountsKnown && ExecutableReady && LinkedAccounts.Count == 0;
    public AsyncCommand RefreshAccountsCommand { get; }
    public AsyncCommand UseAutomaticDetectionCommand { get; }

    public SignalCliConfiguration() : this(SignalExecutable.DetectForApplicationAsync, SignalCliPreferences.Save) { }

    internal SignalCliConfiguration(Func<Task<SignalExecutable?>> detectExecutable, Action<string?> savePreference)
    {
        this.detectExecutable = detectExecutable;
        this.savePreference = savePreference;
        RefreshAccountsCommand = new(RefreshAccountsAsync, () => CanConfigure && ExecutableReady);
        UseAutomaticDetectionCommand = new(() => ConfigureExecutableAsync(null), () => CanConfigure);
    }

    public async Task InitializeAsync()
    {
        try { customExecutable = SignalCliPreferences.Load(); }
        catch
        {
            ExecutableStatus = "Could not read Settings. Choose an executable or use automatic detection.";
            return;
        }
        await ConfigureExecutableAsync(customExecutable, persist: false);
    }

    public Task<SignalExecutable?> ResolveExecutableAsync() => customExecutable == null
        ? detectExecutable()
        : SignalExecutable.ValidateAsync(customExecutable);

    public async Task ConfigureExecutableAsync(string? chosenPath, bool persist = true)
    {
        if (!CanConfigure) return;
        resolving = true;
        Cli = "";
        LinkedAccounts = Array.Empty<string>();
        accountsKnown = false;
        Account = "";
        AccountDiscoveryStatus = "";
        ExecutableVersion = "";
        ExecutableStatus = "Looking for signal-cli…";
        NotifyState();
        try
        {
            // Reset the mode independently of account discovery or a disconnected custom CLI.
            if (chosenPath == null)
            {
                if (persist) savePreference(null);
                customExecutable = null;
                Notify(nameof(AutomaticDetection));
            }
            var executable = chosenPath == null
                ? await detectExecutable()
                : await SignalExecutable.ValidateAsync(chosenPath);
            if (closed) return;
            if (chosenPath != null && executable == null)
            {
                ExecutableStatus = "Invalid executable. Choose a working signal-cli or use automatic detection.";
                return;
            }
            if (persist && chosenPath != null) savePreference(chosenPath);
            customExecutable = chosenPath;
            Notify(nameof(AutomaticDetection));
            Cli = executable?.Path ?? "";
            ExecutableVersion = executable?.Version ?? "";
            ExecutableStatus = executable == null
                ? "signal-cli not found. Install signal-cli or choose its location in Settings."
                : "signal-cli ready";
        }
        catch { ExecutableStatus = "Could not save Settings. Try again."; Cli = ""; }
        finally { resolving = false; NotifyState(); }
        if (ExecutableReady) await RefreshAccountsAsync();
    }

    public async Task RefreshAccountsAsync()
    {
        if (!CanConfigure || !ExecutableReady) return;
        discovering = true;
        AccountDiscoveryStatus = "Looking for linked accounts…";
        NotifyState();
        try
        {
            var accounts = await SignalAccountDiscovery.ListAsync(Cli);
            if (closed) return;
            LinkedAccounts = accounts;
            accountsKnown = true;
            Account = accounts.Count == 1 ? accounts[0]
                : accounts.Contains(Account, StringComparer.Ordinal) ? Account : "";
            AccountDiscoveryStatus = accounts.Count switch
            {
                0 => "Signal is not connected. Choose Connect Signal to get started.",
                1 => "Account ready",
                _ => "Multiple accounts found. Choose the account to send from."
            };
        }
        catch
        {
            if (!closed)
            {
                LinkedAccounts = Array.Empty<string>();
                accountsKnown = false;
                Account = "";
                AccountDiscoveryStatus = "Could not detect accounts. Check Settings and refresh.";
            }
        }
        finally { discovering = false; NotifyState(); }
    }

    internal bool TryBeginLink()
    {
        if (!CanConnect) return false;
        linking = true;
        NotifyState();
        return true;
    }

    internal void EndLink() { linking = false; NotifyState(); }

    public void ReportSelectionError() => ExecutableStatus = "Could not choose executable. Try again.";
    public void SetSending(bool isSending) { locked = isSending; NotifyState(); }
    public void Close() { closed = true; NotifyState(); }

    private void NotifyState()
    {
        Notify(nameof(ExecutableReady));
        Notify(nameof(IsWorking));
        Notify(nameof(CanConfigure));
        Notify(nameof(CanConnect));
        Notify(nameof(NeedsConnection));
        Notify(nameof(HasSelectedAccount));
        RefreshAccountsCommand.NotifyCanExecuteChanged();
        UseAutomaticDetectionCommand.NotifyCanExecuteChanged();
    }
}
