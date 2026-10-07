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
    [Theory]
    [InlineData(MessageStatus.Pending, "Pending")]
    [InlineData(MessageStatus.Sending, "Sending")]
    [InlineData(MessageStatus.Cancelled, "Cancelled")]
    [InlineData(MessageStatus.Sent, "Sent — accepted by signal-cli")]
    [InlineData(MessageStatus.Unknown, "Unknown — check Signal before rescheduling")]
    [InlineData(MessageStatus.UnknownOrFailed, "Unknown/failed — check Signal before rescheduling")]
    [InlineData(MessageStatus.Missed, "Missed — reschedule manually")]
    [InlineData(MessageStatus.Blocked, "Blocked — signal-cli missing; reschedule manually")]
    public void StatusWritesOriginalWireStringAndReadsItBack(MessageStatus status, string wireValue)
    {
        var json = JsonSerializer.Serialize(status);
        Assert.Equal(wireValue, JsonSerializer.Deserialize<string>(json));
        Assert.Equal(status, JsonSerializer.Deserialize<MessageStatus>(json));
    }

    [Theory]
    [InlineData("\"FutureStatus\"")]
    [InlineData("\"pending\"")]
    [InlineData("\"1\"")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task UnrecognizedOrMissingStateLoadsAndNeverDispatches(string? stateJson)
    {
        using var queue = new TestQueue();
        var message = queue.Add(TimeSpan.FromMinutes(-1));
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(message))!.AsObject();
        if (stateJson is null) node.Remove("State");
        else node["State"] = System.Text.Json.Nodes.JsonNode.Parse(stateJson);
        using var cipher = new QueueCipher(queue.Key.ToArray());
        File.WriteAllBytes(Path.Combine(queue.DirectoryPath, "queue.enc"),
            cipher.Encrypt(Encoding.UTF8.GetBytes("[" + node.ToJsonString() + "]")));
        queue.Store.Dispose();
        using var reopened = new EncryptedQueueStore(queue.DirectoryPath, queue.Key.ToArray());
        Assert.Equal(MessageStatus.Unknown, Assert.Single(reopened.Items).State);
        var sender = new FakeSignalSender();
        await new SignalScheduler.Services.MessageDispatcher(reopened, sender).DispatchDueAsync();
        Assert.Empty(sender.Attempts);
        using var persisted = JsonDocument.Parse(cipher.Decrypt(File.ReadAllBytes(Path.Combine(queue.DirectoryPath, "queue.enc"))));
        Assert.Equal("Unknown — check Signal before rescheduling", persisted.RootElement[0].GetProperty("State").GetString());
    }

    [Fact]
    public Task MissingStateLoadsAndNeverDispatches()
        => UnrecognizedOrMissingStateLoadsAndNeverDispatches(null);

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
    [InlineData("Pending", MessageStatus.Pending)]
    [InlineData("Sending", MessageStatus.Unknown)]
    [InlineData("Cancelled", MessageStatus.Cancelled)]
    [InlineData("Sent — accepted by signal-cli", MessageStatus.Sent)]
    [InlineData("Unknown — check Signal before rescheduling", MessageStatus.Unknown)]
    [InlineData("Unknown/failed — check Signal before rescheduling", MessageStatus.UnknownOrFailed)]
    [InlineData("Missed — reschedule manually", MessageStatus.Missed)]
    [InlineData("Blocked — signal-cli missing; reschedule manually", MessageStatus.Blocked)]
    [InlineData("Sent", MessageStatus.Sent)]
    [InlineData("Unknown", MessageStatus.Unknown)]
    [InlineData("UnknownOrFailed", MessageStatus.UnknownOrFailed)]
    [InlineData("Missed", MessageStatus.Missed)]
    [InlineData("Blocked", MessageStatus.Blocked)]
    [InlineData("LegacyUnexpectedStatus", MessageStatus.Unknown)]
    public void LegacyTextOnlyQueueLoadsWithoutMigration(string state, MessageStatus expected)
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
                    DateTimeOffset.UtcNow.AddMinutes(10), MessageStatus.Pending, "account-placeholder", "cli-placeholder",
                    new() { new ImageAttachment("synthetic.png", new byte[] { 1, 2, 3 }) });
                id = message.Id;
                store.Items.Add(message);
                store.Save();
                store.Change(id, MessageStatus.Cancelled);
            }
            using var reopened = new EncryptedQueueStore(directory, key.ToArray());
            var loaded = Assert.Single(reopened.Items);
            Assert.Equal(id, loaded.Id);
            Assert.Equal(MessageStatus.Cancelled, loaded.State);
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
