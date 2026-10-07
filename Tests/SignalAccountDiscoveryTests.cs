using SignalScheduler.Integrations.Signal;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class SignalAccountDiscoveryTests
{
    [Fact]
    public void ReadsNumberOrAciAndRemovesDuplicatesWithoutChoosingAnAccount()
    {
        var result = SignalAccountDiscovery.ParseAccounts("""
            [{"number":"number-placeholder","aci":"aci-placeholder"},
             {"number":null,"aci":"second-aci-placeholder"},
             {"number":"number-placeholder"}]
            """);
        Assert.Equal(new[] { "number-placeholder", "second-aci-placeholder" }, result);
    }

    [Fact]
    public void EmptyListIsSupported()
        => Assert.Empty(SignalAccountDiscovery.ParseAccounts("[]"));

    [Theory]
    [InlineData("not JSON")]
    [InlineData("{}")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    [InlineData("[{\"number\":\"\",\"aci\":null}]")]
    public void MalformedOutputIsRejectedWithoutInventingAnAccount(string output)
        => Assert.ThrowsAny<System.Text.Json.JsonException>(() => SignalAccountDiscovery.ParseAccounts(output));

    [Fact]
    public async Task UsesJsonListCommandWithoutAnAccountArgument()
    {
        using var queue = new TestQueue();
        File.WriteAllText(queue.Executable, """
            #!/bin/sh
            [ "$#" = 3 ] && [ "$1" = '--output' ] && [ "$2" = 'json' ] && [ "$3" = 'listAccounts' ] || exit 2
            printf '%s' '[{"number":"number-placeholder"}]'
            """ + "\n");
        File.SetUnixFileMode(queue.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Assert.Equal(new[] { "number-placeholder" }, await SignalAccountDiscovery.ListAsync(queue.Executable));
    }

    [Fact]
    public async Task FailedCommandDoesNotUseItsOutputOrExposeDiagnostics()
    {
        using var queue = new TestQueue();
        File.WriteAllText(queue.Executable, "#!/bin/sh\nprintf '%s' '[{\"number\":\"number-placeholder\"}]'\nprintf '%s' 'private-diagnostic-placeholder' >&2\nexit 1\n");
        File.SetUnixFileMode(queue.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var error = await Assert.ThrowsAsync<IOException>(() => SignalAccountDiscovery.ListAsync(queue.Executable));
        Assert.DoesNotContain("private-diagnostic-placeholder", error.Message);
    }
}
