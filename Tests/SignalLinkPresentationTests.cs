using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Threading;
using SignalScheduler.ViewModels;
using SignalScheduler.Views;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class SignalLinkPresentationTests
{
    private static string Script(bool succeeds) =>
        "if [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.9'; exit 0; fi\n" +
        "if [ \"$1\" = '--output' ]; then if [ -f \"$0.connected\" ]; then printf '[{\"number\":\"synthetic-account\"}]'; else printf '[]'; fi; exit 0; fi\n" +
        "printf '%s\\n' 'sgnl://linkdevice?uuid=synthetic-device&pub_key=synthetic-key'\n" +
        (succeeds ? "sleep 1\ntouch \"$0.connected\"\n" : "sleep 60\n");

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }

    [AvaloniaFact]
    public async Task SuccessfulLinkDisplaysQrThenSelectsAccountAndRemovesWelcomeState()
    {
        using var queue = new TestQueue();
        SignalDeviceLinkerTests.WriteExecutable(queue.Executable, Script(true));
        var configuration = new SignalCliConfiguration();
        await configuration.ConfigureExecutableAsync(queue.Executable, persist: false);
        Assert.True(configuration.NeedsConnection);
        using var model = new SignalLinkViewModel(configuration);
        var operation = model.StartAsync();
        await WaitUntil(() => model.HasQrCode);
        Assert.True(model.IsConnecting);
        Assert.False(configuration.CanConfigure);
        Assert.False(configuration.HasSelectedAccount);
        Assert.True(model.QrCode!.PixelSize.Width > 200);
        await operation;
        Assert.True(model.Connected);
        Assert.False(model.HasQrCode);
        Assert.False(configuration.NeedsConnection);
        Assert.Equal("synthetic-account", configuration.Account);
        Assert.True(configuration.HasSelectedAccount);
        configuration.Close();
    }

    [AvaloniaFact]
    public async Task CancelRemovesQrAndAllowsFreshAttempt()
    {
        using var queue = new TestQueue();
        SignalDeviceLinkerTests.WriteExecutable(queue.Executable, Script(false));
        var configuration = new SignalCliConfiguration();
        await configuration.ConfigureExecutableAsync(queue.Executable, persist: false);
        using var model = new SignalLinkViewModel(configuration);
        var first = model.StartAsync();
        await WaitUntil(() => model.HasQrCode);
        await model.CancelAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await first;
        Assert.False(model.HasQrCode);
        Assert.False(model.IsConnecting);
        Assert.Contains("canceled", model.Status);
        Assert.True(model.ConnectCommand.CanExecute(null));
        var retry = model.StartAsync();
        await WaitUntil(() => model.HasQrCode);
        await model.CancelAsync();
        await retry;
        configuration.Close();
    }

    [AvaloniaFact]
    public async Task ExpiredCodeIsRemovedAndCanBeRetried()
    {
        using var queue = new TestQueue();
        SignalDeviceLinkerTests.WriteExecutable(queue.Executable, Script(false));
        var configuration = new SignalCliConfiguration();
        await configuration.ConfigureExecutableAsync(queue.Executable, persist: false);
        using var model = new SignalLinkViewModel(configuration, timeoutSeconds: 1);
        await model.StartAsync();
        Assert.False(model.HasQrCode);
        Assert.Contains("expired", model.Status);
        Assert.True(model.ConnectCommand.CanExecute(null));
        configuration.Close();
    }

    [AvaloniaFact]
    public async Task ClosingLinkWindowCancelsAttemptBeforeClosing()
    {
        using var queue = new TestQueue();
        SignalDeviceLinkerTests.WriteExecutable(queue.Executable, Script(false));
        var configuration = new SignalCliConfiguration();
        await configuration.ConfigureExecutableAsync(queue.Executable, persist: false);
        var window = new SignalLinkWindow(configuration);
        var model = (SignalLinkViewModel)window.DataContext!;
        window.Show();
        await WaitUntil(() => model.HasQrCode);
        if (Environment.GetEnvironmentVariable("SIGNAL_SCHEDULER_PREVIEW_DIR") is { } previewDirectory)
        {
            Dispatcher.UIThread.RunJobs();
            Directory.CreateDirectory(previewDirectory);
            using var frame = window.CaptureRenderedFrame();
            frame!.Save(Path.Combine(previewDirectory, "signal-link-preview.png"));
        }
        window.Close();
        await WaitUntil(() => !window.IsVisible);
        Assert.False(model.IsConnecting);
        Assert.False(model.HasQrCode);
        Assert.False(configuration.IsWorking);
        configuration.Close();
    }

    [AvaloniaFact]
    public async Task WelcomeAndComposerSwitchThroughBindingsWithoutOpeningRealQueue()
    {
        using var queue = new TestQueue();
        SignalDeviceLinkerTests.WriteExecutable(queue.Executable, Script(false));
        var view = new MainWindow();
        var owner = (MainWindowViewModel)view.DataContext!;
        var content = view.Content;
        view.Content = null;
        var host = new Window { Content = content, DataContext = owner, Width = 640, Height = 760 };
        host.Show();
        try
        {
            await owner.Signal.ConfigureExecutableAsync(queue.Executable, persist: false);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.FindControl<Border>("SignalWelcome")!.IsVisible);
            Assert.False(view.FindControl<Border>("MessageComposer")!.IsVisible);
            File.WriteAllText(queue.Executable + ".connected", "synthetic");
            await owner.Signal.RefreshAccountsAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.FindControl<Border>("SignalWelcome")!.IsVisible);
            Assert.True(view.FindControl<Border>("MessageComposer")!.IsVisible);
        }
        finally { host.Close(); owner.TryClose(); }
    }

    [AvaloniaFact]
    public async Task ExistingAccountNeedsNoOnboardingAndDiscoveryFailureDoesNotPromptRelinking()
    {
        using var queue = new TestQueue();
        SignalDeviceLinkerTests.WriteExecutable(queue.Executable, "if [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.9'; else printf '[{\"number\":\"synthetic-account\"}]'; fi\n");
        var configuration = new SignalCliConfiguration();
        await configuration.ConfigureExecutableAsync(queue.Executable, persist: false);
        Assert.False(configuration.NeedsConnection);
        Assert.True(configuration.HasSelectedAccount);
        SignalDeviceLinkerTests.WriteExecutable(queue.Executable, "if [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.9'; else exit 1; fi\n");
        await configuration.RefreshAccountsAsync();
        Assert.False(configuration.NeedsConnection);
        Assert.False(configuration.HasSelectedAccount);
        Assert.Contains("refresh", configuration.AccountDiscoveryStatus);
        configuration.Close();
    }
}
