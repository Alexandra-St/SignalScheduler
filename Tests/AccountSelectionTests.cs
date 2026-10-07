using System.Reflection;
using SignalScheduler.ViewModels;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class AccountSelectionTests
{
    [Theory]
    [InlineData("[]", "", false)]
    [InlineData("[{\"number\":\"account-one\"}]", "account-one", true)]
    [InlineData("[{\"number\":\"account-one\"},{\"number\":\"account-two\"}]", "", false)]
    [InlineData("invalid", "", false)]
    public async Task DiscoveryControlsSelectionAndScheduling(string output, string selected, bool enabled)
    {
        using var queue = new TestQueue();
        File.WriteAllText(queue.Executable, "#!/bin/sh\nprintf '%s' '" + output + "'\n");
        File.SetUnixFileMode(queue.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var model = new MainWindowViewModel { Cli = queue.Executable };
        typeof(MainWindowViewModel).GetField("canSchedule", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, true);
        await model.RefreshAccountsAsync();
        Assert.Equal(selected, model.Account);
        Assert.Equal(enabled, model.CanSchedule);
        if (model.LinkedAccounts.Count > 1)
        {
            model.Account = "account-two";
            Assert.True(model.CanSchedule);
        }
        model.Account = "unlisted-account";
        Assert.False(model.CanSchedule);
        model.Cli = queue.Executable + "-changed";
        Assert.Empty(model.LinkedAccounts);
        Assert.Equal("", model.Account);
        Assert.False(model.CanSchedule);
        model.TryClose();
    }
}
