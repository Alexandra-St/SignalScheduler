using SignalScheduler.ViewModels;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class AccountSelectionTests
{
    [Fact]
    public async Task AutomaticModeCanRecoverAfterCustomDeviceIsRemoved()
    {
        using var queue = new TestQueue();
        File.WriteAllText(queue.Executable, "#!/bin/sh\nif [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.9'; else printf '[]'; fi\n");
        File.SetUnixFileMode(queue.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string? saved = "custom";
        var model = new SignalCliConfiguration(() => Task.FromResult<SignalScheduler.Integrations.Signal.SignalExecutable?>(new(queue.Executable, "0.14.9")), path => saved = path);
        await model.ConfigureExecutableAsync(queue.Executable);
        Assert.False(model.AutomaticDetection);
        Assert.False(model.HasSelectedAccount);
        Assert.True(model.UseAutomaticDetectionCommand.CanExecute(null));
        await model.ConfigureExecutableAsync(null);
        Assert.True(model.AutomaticDetection);
        Assert.Null(saved);
        Assert.True(model.CanConfigure);
        Assert.True(model.NeedsConnection);
        model.Close();
    }

    [Theory]
    [InlineData("[]", "", false)]
    [InlineData("[{\"number\":\"account-one\"}]", "account-one", true)]
    [InlineData("[{\"number\":\"account-one\"},{\"number\":\"account-two\"}]", "", false)]
    [InlineData("invalid", "", false)]
    public async Task DiscoveryControlsAccountReadiness(string output, string selected, bool enabled)
    {
        using var queue = new TestQueue();
        File.WriteAllText(queue.Executable, "#!/bin/sh\nif [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.1'; exit 0; fi\nprintf '%s' '" + output + "'\n");
        File.SetUnixFileMode(queue.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var model = new SignalCliConfiguration();
        await model.ConfigureExecutableAsync(queue.Executable, persist: false);
        Assert.Equal(selected, model.Account);
        Assert.Equal(enabled, model.HasSelectedAccount);
        if (model.LinkedAccounts.Count > 1)
        {
            model.Account = "account-two";
            Assert.True(model.HasSelectedAccount);
        }
        model.Account = "unlisted-account";
        Assert.False(model.HasSelectedAccount);
        await model.ConfigureExecutableAsync(queue.Executable + "-changed", persist: false);
        Assert.Empty(model.LinkedAccounts);
        Assert.Equal("", model.Account);
        Assert.False(model.HasSelectedAccount);
        model.Close();
    }
}
