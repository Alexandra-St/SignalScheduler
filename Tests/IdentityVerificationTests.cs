using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SignalScheduler.Integrations.Signal;
using SignalScheduler.Models;
using SignalScheduler.ViewModels;
using SignalScheduler.Views;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class IdentityVerificationTests
{
    private const string SafetyNumber = "12345 12345 12345 12345 12345 12345 12345 12345 12345 12345 12345 12345";
    private const string Sender = "+12025550100";
    private const string Recipient = "+12025550123";

    private static async Task<string> CreateFakeCliAsync(string status = "UNTRUSTED", string safetyNumber = SafetyNumber)
    {
        var folder = Path.Combine(Path.GetTempPath(), "signal-scheduler-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "fake-cli");
        await File.WriteAllTextAsync(path + ".status", status);
        await File.WriteAllTextAsync(path + ".safety", safetyNumber);
        var script = """
            #!/bin/sh
            printf '%s\n' "$@" >> "$0.args"
            if [ "$5" = 'listIdentities' ]; then
              printf '[{"uuid":"synthetic-uuid","fingerprint":"synthetic-fingerprint","trustLevel":"%s","safetyNumber":"%s"}]' "$(cat "$0.status")" "$(cat "$0.safety")"
              exit 0
            fi
            if [ "$3" = 'trust' ]; then
              [ "$1" = '-a' ] && [ "$2" = '@SENDER@' ] && [ "$4" = '--verified-safety-number' ] && [ "$5" = "$(cat "$0.safety")" ] && [ "$6" = '@RECIPIENT@' ] || exit 7
              printf 'TRUSTED_VERIFIED' > "$0.status"
              exit 0
            fi
            exit 9
            """.Replace("@SENDER@", Sender).Replace("@RECIPIENT@", Recipient);
        await File.WriteAllTextAsync(path, script);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return folder;
    }

    private static SignalCliConfiguration Configuration(string path) =>
        new(() => Task.FromResult<SignalExecutable?>(new(path, "0.14.9")), _ => { }, new SignalCliAdapter());

    private static ScheduledMessage Message() => new(Guid.NewGuid(), Recipient, "synthetic message",
        DateTimeOffset.UtcNow.AddMinutes(-1), MessageStatus.RequiresVerification, Sender, "historical-cli");

    [Fact]
    public async Task ReadUsesTheMessageAccountAndRecipientAndDoesNotTrust()
    {
        var folder = await CreateFakeCliAsync();
        try
        {
            var path = Path.Combine(folder, "fake-cli");
            var identity = await Configuration(path).ReadIdentityForVerificationAsync(Message());
            Assert.Equal("UNTRUSTED", identity.TrustStatus);
            Assert.Equal(SafetyNumber, identity.SafetyNumber);
            var args = (await File.ReadAllLinesAsync(path + ".args")).Where(line => line.Length > 0).ToArray();
            Assert.Equal(new[] { "--output", "json", "-a", Sender, "listIdentities", "-n", Recipient }, args);
            Assert.Equal("UNTRUSTED", await File.ReadAllTextAsync(path + ".status"));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task ChangedSafetyNumberIsRejectedBeforeTrust()
    {
        var folder = await CreateFakeCliAsync();
        try
        {
            var path = Path.Combine(folder, "fake-cli");
            var configuration = Configuration(path);
            var shown = await configuration.ReadIdentityForVerificationAsync(Message());
            await File.WriteAllTextAsync(path + ".safety", "99999 99999 99999 99999 99999 99999 99999 99999 99999 99999 99999 99999");

            Assert.False(await configuration.TrustVerifiedIdentityForMessageAsync(Message(), shown.SafetyNumber));
            Assert.DoesNotContain("trust", await File.ReadAllTextAsync(path + ".args"));
            Assert.Equal("UNTRUSTED", await File.ReadAllTextAsync(path + ".status"));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task CliErrorsDoNotExposeRawOutput()
    {
        var folder = await CreateFakeCliAsync();
        try
        {
            var path = Path.Combine(folder, "fake-cli");
            await File.WriteAllTextAsync(path, "#!/bin/sh\nprintf 'synthetic secret output'\nexit 7\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var exception = await Assert.ThrowsAsync<IOException>(() => new SignalCliAdapter()
                .ListIdentitiesAsync(path, Sender, Recipient));
            Assert.DoesNotContain("synthetic secret", exception.Message);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task TrustErrorsDoNotExposeRawOutput()
    {
        var folder = await CreateFakeCliAsync();
        try
        {
            var path = Path.Combine(folder, "fake-cli");
            await File.WriteAllTextAsync(path, "#!/bin/sh\nprintf 'synthetic secret output'\nexit 7\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var exception = await Assert.ThrowsAsync<IOException>(() => new SignalCliAdapter()
                .TrustVerifiedIdentityAsync(path, Sender, Recipient, SafetyNumber));
            Assert.DoesNotContain("synthetic secret", exception.Message);
        }
        finally { Directory.Delete(folder, true); }
    }

    [AvaloniaFact]
    public async Task DetailsRequireExplicitConfirmationAndVerifyTheSameIdentity()
    {
        var folder = await CreateFakeCliAsync();
        var owner = new MainWindowViewModel();
        var message = Message();
        using var queue = new TestQueue();
        queue.Store.Add(message);
        using var model = new MessageViewModel(message, owner, Configuration(Path.Combine(folder, "fake-cli")));
        var window = new MessageDetailsWindow(owner, model);
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var buttons = window.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.True(buttons.Single(button => Equals(button.Content, "Verify identity")).IsVisible);
            Assert.False(buttons.Single(button => Equals(button.Content, "I've verified the safety number")).IsVisible);

            await model.VerifyIdentityCommand.ExecuteAsync(); Dispatcher.UIThread.RunJobs();
            Assert.True(model.VerificationPanelVisible);
            Assert.Equal("Compare this safety number with the recipient through a separate trusted channel. If it does not match, do not confirm.", model.VerificationMessage);
            Assert.Equal(SafetyNumber, model.SafetyNumber);
            Assert.Equal(SafetyNumber, window.FindControl<SelectableTextBlock>("SafetyNumberText")?.Text);
            Assert.True(buttons.Single(button => Equals(button.Content, "I've verified the safety number")).IsVisible);
            Assert.Contains("separate trusted channel", model.VerificationMessage);
            Assert.Equal("UNTRUSTED", await File.ReadAllTextAsync(Path.Combine(folder, "fake-cli.status")));

            await model.ConfirmIdentityCommand.ExecuteAsync(); Dispatcher.UIThread.RunJobs();
            Assert.Equal("TRUSTED_VERIFIED", model.VerificationTrustStatus);
            Assert.Contains("No message was sent", model.VerificationMessage);
            Assert.False(model.HasSafetyNumber);
            Assert.True(model.CanScheduleAgain);
            Assert.True(buttons.Single(button => Equals(button.Content, "Schedule again")).IsVisible);
            Assert.Equal("TRUSTED_VERIFIED", await File.ReadAllTextAsync(Path.Combine(folder, "fake-cli.status")));
            var args = await File.ReadAllTextAsync(Path.Combine(folder, "fake-cli.args"));
            Assert.Contains("--verified-safety-number", args);
            Assert.Contains(SafetyNumber, args);
            var recorded = (await File.ReadAllLinesAsync(Path.Combine(folder, "fake-cli.args")))
                .Where(line => line.Length > 0).ToArray();
            var trustIndex = Array.IndexOf(recorded, "trust");
            Assert.Equal(new[] { "-a", Sender, "trust", "--verified-safety-number", SafetyNumber, Recipient }, recorded[(trustIndex - 2)..(trustIndex + 4)]);
            Assert.DoesNotContain(SafetyNumber, message.Text + message.Recipient + model.StatusHint);
            Assert.Equal(MessageStatus.RequiresVerification, message.State);
            Assert.Equal(MessageStatus.RequiresVerification, Assert.Single(queue.Store.Items).State);
            Assert.DoesNotContain(SafetyNumber, string.Join("|", queue.Store.Items.Select(item => item.Text + item.Recipient)));

            await model.CheckIdentityAgainCommand.ExecuteAsync(); Dispatcher.UIThread.RunJobs();
            Assert.True(model.CanScheduleAgain);
            Assert.True(buttons.Single(button => Equals(button.Content, "Schedule again")).IsVisible);
            Assert.Contains("still verified", model.VerificationMessage);
            Assert.Equal(MessageStatus.RequiresVerification, Assert.Single(queue.Store.Items).State);
            await File.WriteAllTextAsync(Path.Combine(folder, "fake-cli.status"), "UNTRUSTED");
            Assert.False(await model.RecheckIdentityForScheduleAsync());
            Assert.False(model.CanScheduleAgain);
            Assert.False(buttons.Single(button => Equals(button.Content, "Schedule again")).IsVisible);
            Assert.Equal("UNTRUSTED", model.VerificationTrustStatus);
            Assert.True(model.HasSafetyNumber);
            Assert.Contains("Compare this safety number", model.VerificationMessage);
            Assert.Equal(MessageStatus.RequiresVerification, Assert.Single(queue.Store.Items).State);
        }
        finally
        {
            window.Close(); owner.TryClose(); Directory.Delete(folder, true);
        }
    }

    [AvaloniaFact]
    public async Task AlreadyVerifiedIdentityEnablesScheduleAgainAfterReopeningDetails()
    {
        var folder = await CreateFakeCliAsync(status: "TRUSTED_VERIFIED");
        var owner = new MainWindowViewModel();
        var message = Message();
        var signal = Configuration(Path.Combine(folder, "fake-cli"));
        using var firstModel = new MessageViewModel(message, owner, signal);
        var firstWindow = new MessageDetailsWindow(owner, firstModel);
        try
        {
            firstWindow.Show(); Dispatcher.UIThread.RunJobs();
            var firstButtons = firstWindow.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.False(firstButtons.Single(button => Equals(button.Content, "Schedule again")).IsVisible);

            await firstModel.VerifyIdentityCommand.ExecuteAsync(); Dispatcher.UIThread.RunJobs();
            Assert.Equal("TRUSTED_VERIFIED", firstModel.VerificationTrustStatus);
            Assert.True(firstModel.CanScheduleAgain);
            Assert.True(firstButtons.Single(button => Equals(button.Content, "Schedule again")).IsVisible);
            Assert.Contains("already verified", firstModel.VerificationMessage);
            Assert.DoesNotContain("trust", await File.ReadAllTextAsync(Path.Combine(folder, "fake-cli.args")));

            firstWindow.Close(); Dispatcher.UIThread.RunJobs();
            using var reopenedModel = new MessageViewModel(message, owner, signal);
            var reopenedWindow = new MessageDetailsWindow(owner, reopenedModel);
            try
            {
                reopenedWindow.Show(); Dispatcher.UIThread.RunJobs();
                var reopenedButtons = reopenedWindow.GetVisualDescendants().OfType<Button>().ToArray();
                Assert.False(reopenedButtons.Single(button => Equals(button.Content, "Schedule again")).IsVisible);

                await reopenedModel.VerifyIdentityCommand.ExecuteAsync(); Dispatcher.UIThread.RunJobs();
                Assert.Equal("TRUSTED_VERIFIED", reopenedModel.VerificationTrustStatus);
                Assert.True(reopenedModel.CanScheduleAgain);
                Assert.True(reopenedButtons.Single(button => Equals(button.Content, "Schedule again")).IsVisible);
                Assert.Contains("already verified", reopenedModel.VerificationMessage);
                Assert.DoesNotContain("trust", await File.ReadAllTextAsync(Path.Combine(folder, "fake-cli.args")));
            }
            finally { reopenedWindow.Close(); }
        }
        finally
        {
            firstWindow.Close(); owner.TryClose(); Directory.Delete(folder, true);
        }
    }
}
