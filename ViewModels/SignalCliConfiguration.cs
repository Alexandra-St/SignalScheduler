using SignalScheduler.Infrastructure;
using SignalScheduler.Integrations.Signal;
using SignalScheduler.Presentation;

namespace SignalScheduler.ViewModels;

public sealed class SignalCliConfiguration : ObservableObject
{
    private string? customExecutable;
    private string cli = "", version = "", account = "";
    private string executableStatus = "Looking for signal-cli…", accountDiscoveryStatus = "";
    private IReadOnlyList<string> linkedAccounts = Array.Empty<string>();
    private bool resolving, discovering, locked, closed;

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
    public bool IsWorking => resolving || discovering;
    public bool CanConfigure => !closed && !locked && !IsWorking;
    public bool HasSelectedAccount => ExecutableReady && !discovering && LinkedAccounts.Contains(Account, StringComparer.Ordinal);
    public AsyncCommand RefreshAccountsCommand { get; }
    public AsyncCommand UseAutomaticDetectionCommand { get; }

    public SignalCliConfiguration()
    {
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
        ? SignalExecutable.DetectAsync(Environment.GetEnvironmentVariable("PATH"))
        : SignalExecutable.ValidateAsync(customExecutable);

    public async Task ConfigureExecutableAsync(string? chosenPath, bool persist = true)
    {
        if (!CanConfigure) return;
        resolving = true;
        Cli = "";
        LinkedAccounts = Array.Empty<string>();
        Account = "";
        AccountDiscoveryStatus = "";
        ExecutableVersion = "";
        ExecutableStatus = "Looking for signal-cli…";
        NotifyState();
        try
        {
            var executable = chosenPath == null
                ? await SignalExecutable.DetectAsync(Environment.GetEnvironmentVariable("PATH"))
                : await SignalExecutable.ValidateAsync(chosenPath);
            if (closed) return;
            if (chosenPath != null && executable == null)
            {
                ExecutableStatus = "Invalid executable. Choose a working signal-cli or use automatic detection.";
                return;
            }
            if (persist) SignalCliPreferences.Save(chosenPath);
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
            if (!closed)
            {
                LinkedAccounts = Array.Empty<string>();
                Account = "";
                AccountDiscoveryStatus = "Could not detect accounts. Check Settings and refresh.";
            }
        }
        finally { discovering = false; NotifyState(); }
    }

    public void ReportSelectionError() => ExecutableStatus = "Could not choose executable. Try again.";
    public void SetSending(bool isSending) { locked = isSending; NotifyState(); }
    public void Close() { closed = true; NotifyState(); }

    private void NotifyState()
    {
        Notify(nameof(ExecutableReady));
        Notify(nameof(IsWorking));
        Notify(nameof(CanConfigure));
        Notify(nameof(HasSelectedAccount));
        RefreshAccountsCommand.NotifyCanExecuteChanged();
        UseAutomaticDetectionCommand.NotifyCanExecuteChanged();
    }
}
