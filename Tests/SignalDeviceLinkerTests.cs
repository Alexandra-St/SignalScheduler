using SignalScheduler.Integrations.Signal;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class SignalDeviceLinkerTests
{
    private const string Link = "sgnl://linkdevice?uuid=synthetic-device&pub_key=synthetic-key";
    internal static void WriteExecutable(string path, string script)
    {
        File.WriteAllText(path, "#!/bin/sh\n" + script);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task StreamsCodeBeforeExitAndPassesDeviceNameAsOneArgument()
    {
        using var queue = new TestQueue();
        WriteExecutable(queue.Executable, "[ \"$1\" = link ] && [ \"$2\" = -n ] && [ \"$3\" = 'Signal Scheduler' ] || exit 4\nprintf '%s\\n' '" + Link + "'\nsleep 1\n");
        var code = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = SignalDeviceLinker.LinkAsync(queue.Executable, uri => { code.SetResult(uri); return Task.CompletedTask; }, default);
        Assert.Equal(Link, await code.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(operation.IsCompleted);
        await operation;
    }

    [Fact]
    public async Task CancelTerminatesProcessAndClearsWaitingReaders()
    {
        using var queue = new TestQueue();
        WriteExecutable(queue.Executable, "printf '%s\\n' '" + Link + "'\nsleep 60\n");
        using var cancel = new CancellationTokenSource();
        var code = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = SignalDeviceLinker.LinkAsync(queue.Executable, _ => { code.SetResult(); return Task.CompletedTask; }, cancel.Token);
        await code.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task TimeoutTerminatesWaitingProcess()
    {
        using var queue = new TestQueue();
        WriteExecutable(queue.Executable, "sleep 60\n");
        await Assert.ThrowsAsync<TimeoutException>(() => SignalDeviceLinker.LinkAsync(queue.Executable, _ => Task.CompletedTask, default, 1).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CallbackFailureDoesNotLeaveProcessWaiting()
    {
        using var queue = new TestQueue();
        WriteExecutable(queue.Executable, "printf '%s\\n' '" + Link + "'\nsleep 60\n");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SignalDeviceLinker.LinkAsync(queue.Executable,
            _ => throw new InvalidOperationException("Synthetic render failure"), default).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData("exit 0")]
    [InlineData("printf 'private diagnostic'; exit 2")]
    public async Task FailedOrMissingCodeDoesNotExposeDiagnostics(string script)
    {
        using var queue = new TestQueue();
        WriteExecutable(queue.Executable, script);
        var error = await Assert.ThrowsAsync<IOException>(() => SignalDeviceLinker.LinkAsync(queue.Executable, _ => Task.CompletedTask, default));
        Assert.DoesNotContain("private diagnostic", error.Message);
    }

    [Theory]
    [InlineData("sgnl://linkdevice?uuid=x&pub_key=y", true)]
    [InlineData("https://linkdevice?uuid=x&pub_key=y", false)]
    [InlineData("sgnl://linkdevice?uuid=x", false)]
    [InlineData("sgnl://linkdevice?uuid=&pub_key=y", false)]
    [InlineData("sgnl://linkdevice:123?uuid=x&pub_key=y", false)]
    [InlineData("sgnl://other?uuid=x&pub_key=y", false)]
    [InlineData("sgnl://linkdevice?uuid=x&pub_key=y#secret", false)]
    public void AcceptsOnlyDeviceLinkUris(string value, bool valid)
        => Assert.Equal(valid, SignalDeviceLinker.TryReadLinkUri(value, out _));
}
