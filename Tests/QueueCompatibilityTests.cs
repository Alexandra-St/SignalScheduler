using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SignalScheduler.Models;
using SignalScheduler.Persistence;
using SignalScheduler.Security;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class QueueCompatibilityTests
{
    [Fact]
    public void NewCipherReadsLegacyEnvelopeAndLegacyCipherReadsNewEnvelope()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("Synthetic queue content");
        using var cipher = new QueueCipher(key);
        var legacy = LegacyEncrypt(key, plaintext);
        Assert.Equal(plaintext, cipher.Decrypt(legacy));
        var current = cipher.Encrypt(plaintext);
        var decoded = new byte[current.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(current.AsSpan(0, 12), current.AsSpan(28), current.AsSpan(12, 16), decoded);
        Assert.Equal(plaintext, decoded);
        Assert.NotEqual(current[..12], cipher.Encrypt(plaintext)[..12]);
    }

    [Fact]
    public void TamperedCiphertextIsRejected()
    {
        using var cipher = new QueueCipher(RandomNumberGenerator.GetBytes(32));
        var encrypted = cipher.Encrypt(Encoding.UTF8.GetBytes("Synthetic"));
        encrypted[^1] ^= 1;
        Assert.Throws<AuthenticationTagMismatchException>(() => cipher.Decrypt(encrypted));
        Assert.Throws<InvalidDataException>(() => cipher.Decrypt(new byte[27]));
    }

    [Theory]
    [InlineData(MessageStates.Pending, MessageStates.Pending)]
    [InlineData(MessageStates.Sending, MessageStates.Unknown)]
    [InlineData(MessageStates.Cancelled, MessageStates.Cancelled)]
    [InlineData(MessageStates.Sent, MessageStates.Sent)]
    [InlineData(MessageStates.UnknownOrFailed, MessageStates.UnknownOrFailed)]
    public void LegacyTextOnlyQueueLoadsWithoutMigration(string state, string expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "signal-scheduler-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var id = Guid.NewGuid();
            var due = DateTimeOffset.UtcNow.AddHours(1);
            // Original JSON properties, intentionally without the later Attachments property.
            var legacy = JsonSerializer.SerializeToUtf8Bytes(new[]
            {
                new { Id = id, Recipient = "recipient-placeholder", Text = "Synthetic", Due = due,
                    State = state, Account = "account-placeholder", Cli = "cli-placeholder" }
            });
            File.WriteAllBytes(Path.Combine(directory, "queue.enc"), LegacyEncrypt(key, legacy));
            using var store = new EncryptedQueueStore(directory, key);
            var message = Assert.Single(store.Items);
            Assert.Equal(id, message.Id);
            Assert.Equal(due, message.Due);
            Assert.Equal(expected, message.State);
            Assert.Null(message.Attachments);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void AttachmentBytesAndCancellationSurviveReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), "signal-scheduler-tests", Guid.NewGuid().ToString("N"));
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            Guid id;
            using (var store = new EncryptedQueueStore(directory, key.ToArray()))
            {
                var message = new ScheduledMessage(Guid.NewGuid(), "recipient-placeholder", "",
                    DateTimeOffset.UtcNow.AddMinutes(10), MessageStates.Pending, "account-placeholder", "cli-placeholder",
                    new() { new ImageAttachment("synthetic.png", new byte[] { 1, 2, 3 }) });
                id = message.Id;
                store.Items.Add(message);
                store.Save();
                store.Change(id, MessageStates.Cancelled);
            }
            using var reopened = new EncryptedQueueStore(directory, key.ToArray());
            var loaded = Assert.Single(reopened.Items);
            Assert.Equal(id, loaded.Id);
            Assert.Equal(MessageStates.Cancelled, loaded.State);
            Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(loaded.Attachments!).Data);
            Assert.DoesNotContain("recipient-placeholder", Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "queue.enc"))));
        }
        finally { Directory.Delete(directory, true); CryptographicOperations.ZeroMemory(key); }
    }

    [Fact]
    public void QueuePermissionsAndKeyDisposalRemainUserRestricted()
    {
        var directory = Path.Combine(Path.GetTempPath(), "signal-scheduler-tests", Guid.NewGuid().ToString("N"));
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            using (var store = new EncryptedQueueStore(directory, key))
            {
                store.Save();
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(directory));
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(Path.Combine(directory, "queue.enc")));
            }
            Assert.All(key, value => Assert.Equal((byte)0, value));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static byte[] LegacyEncrypt(byte[] key, byte[] plaintext)
    {
        var bytes = new byte[28 + plaintext.Length];
        RandomNumberGenerator.Fill(bytes.AsSpan(0, 12));
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(bytes.AsSpan(0, 12), plaintext, bytes.AsSpan(28), bytes.AsSpan(12, 16));
        return bytes;
    }
}
