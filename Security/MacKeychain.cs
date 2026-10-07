using System.Security.Cryptography;
using SignalScheduler.Infrastructure;

namespace SignalScheduler.Security;

public static class MacKeychain
{
    public static async Task<byte[]> GetOrCreateKeyAsync()
    {
        const string service = "local.SignalScheduler.queue";
        var found = await ProcessRunner.RunAsync("/usr/bin/security",
            new[] { "find-generic-password", "-s", service, "-a", Environment.UserName, "-w" });
        if (found.Code == 0) return Convert.FromBase64String(found.Output.Trim());
        if (found.Code != 44)
            throw new IOException("Keychain access failed. Unlock Keychain and reopen the app.");

        var key = RandomNumberGenerator.GetBytes(32);
        var added = await ProcessRunner.RunAsync("/usr/bin/security",
            new[] { "add-generic-password", "-s", service, "-a", Environment.UserName, "-w", Convert.ToBase64String(key) });
        if (added.Code != 0) throw new IOException("Could not save encryption key to Keychain.");
        return key;
    }
}
