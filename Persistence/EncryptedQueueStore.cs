using System.Text.Json;
using SignalScheduler.Models;
using SignalScheduler.Security;

namespace SignalScheduler.Persistence;

public sealed class EncryptedQueueStore : IDisposable
{
    private readonly string path;
    private readonly QueueCipher cipher;
    private readonly FileStream instanceLock;
    public List<ScheduledMessage> Items { get; private set; } = new();

    public EncryptedQueueStore(string directory, byte[] key)
    {
        Directory.CreateDirectory(directory);
        File.SetUnixFileMode(directory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        path = Path.Combine(directory, "queue.enc");
        cipher = new QueueCipher(key);
        instanceLock = new FileStream(Path.Combine(directory, "instance.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(path))
        {
            var plaintext = cipher.Decrypt(File.ReadAllBytes(path));
            Items = JsonSerializer.Deserialize<List<ScheduledMessage>>(plaintext)
                ?? throw new InvalidDataException("Invalid queue");
            Items = Items.Select(message => message.State == MessageStatus.Sending
                ? message with { State = MessageStatus.Unknown } : message).ToList();
            Save();
        }
    }

    public void Change(Guid id, MessageStatus state)
    {
        Items = Items.Select(message => message.Id == id ? message with { State = state } : message).ToList();
        Save();
    }

    public void Save()
    {
        var bytes = cipher.Encrypt(JsonSerializer.SerializeToUtf8Bytes(Items));
        var temporary = path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            file.Write(bytes);
            file.Flush(true);
        }
        File.Move(temporary, path, true);
    }

    public void Dispose()
    {
        cipher.Dispose();
        instanceLock.Dispose();
    }
}
