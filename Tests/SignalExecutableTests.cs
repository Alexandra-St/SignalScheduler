using SignalScheduler.Integrations.Signal;
using SignalScheduler.Services;
using SignalScheduler.ViewModels;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class SignalExecutableTests
{
    private static void Script(string path, string text, bool executable = true)
    {
        File.WriteAllText(path, "#!/bin/sh\n" + text + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite |
            (executable ? UnixFileMode.UserExecute : 0));
    }

    [Theory]
    [InlineData("printf 'signal-cli 0.14.1\n'", true, true)]
    [InlineData("printf 'signal-cli 0.14.1\n'", false, false)]
    [InlineData("printf 'different-app 1.2.3\n'", true, false)]
    [InlineData("printf 'signal-cli 0.14.1\n'; exit 1", true, false)]
    public async Task ValidatesExecutableAndVersion(string command, bool executable, bool expected)
    {
        using var queue = new TestQueue();
        Script(queue.Executable, "[ \"$1\" = '--version' ] || exit 2\n" + command, executable);
        var found = await SignalExecutable.ValidateAsync(queue.Executable);
        Assert.Equal(expected, found != null);
        if (found != null) Assert.Equal("0.14.1", found.Version);
    }

    [Fact]
    public async Task SkipsInvalidPathCandidateAndUsesFallback()
    {
        using var queue = new TestQueue();
        var bad = Path.Combine(queue.DirectoryPath, "signal-cli");
        Script(bad, "printf 'not signal-cli'");
        Script(queue.Executable, "printf 'signal-cli 0.14.1'");
        Assert.Equal(queue.Executable, (await SignalExecutable.DetectAsync(queue.DirectoryPath, new[] { queue.Executable }))!.Path);
        Script(bad, "printf 'signal-cli 0.14.2'");
        Assert.Equal(bad, (await SignalExecutable.DetectAsync(queue.DirectoryPath, new[] { queue.Executable }))!.Path);
        Assert.Null(await SignalExecutable.DetectAsync("relative-directory", Array.Empty<string>()));
        Assert.Null(await SignalExecutable.ValidateAsync(queue.Executable + "-missing"));
    }

    [Fact]
    public async Task SettingsValidatesBeforeDiscoveringAccountsAndDisablesInvalidSelection()
    {
        using var queue = new TestQueue();
        Script(queue.Executable, "if [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.1'; else printf '[{\"number\":\"account-one\"}]'; fi");
        var model = new MainWindowViewModel();
        await model.ConfigureExecutableAsync(queue.Executable, persist: false);
        Assert.False(model.AutomaticDetection);
        Assert.True(model.ExecutableReady);
        Assert.Equal("account-one", model.Account);
        Assert.Equal("0.14.1", model.ExecutableVersion);
        await model.ConfigureExecutableAsync(queue.Executable + "-missing", persist: false);
        Assert.False(model.ExecutableReady);
        Assert.False(model.CanSchedule);
        Assert.Empty(model.LinkedAccounts);
        Assert.Contains("Invalid executable", model.ExecutableStatus);
        model.TryClose();
    }

    [Fact]
    public async Task QueueUsesCurrentExecutableWhenHistoricalPathIsMissing()
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromSeconds(-1));
        queue.Store.Items[0] = message with { Cli = queue.Executable + "-old-missing" };
        var sender = new FakeSignalSender();
        await new MessageDispatcher(queue.Store, sender, () => queue.Executable).DispatchDueAsync();
        Assert.Single(sender.Attempts);
    }

    [Fact]
    public async Task DeliveryUsesResolvedPathRatherThanQueuePath()
    {
        using var queue = new TestQueue();
        Script(queue.Executable, "[ \"$1\" = '-a' ] || exit 2\ncat >/dev/null\nprintf 'synthetic-result'");
        var sender = new ConfiguredSignalSender(() => Task.FromResult<SignalExecutable?>(new(queue.Executable, "0.14.1")));
        var result = await sender.SendAsync(queue.Add(TimeSpan.Zero) with { Cli = "/missing/historical-cli" });
        Assert.Equal(0, result.Code);
        Assert.Equal("synthetic-result", result.Output);
        await Assert.ThrowsAsync<IOException>(() => new ConfiguredSignalSender(() => Task.FromResult<SignalExecutable?>(null)).SendAsync(queue.Add(TimeSpan.Zero)));
    }
}
