using System.Text.Json;
using SignalScheduler.Models;
using SignalScheduler.Security;

namespace SignalScheduler.Persistence;

public sealed class EncryptedQueueStore : IDisposable
{
    private readonly string path;
    private readonly QueueCipher cipher;
    private readonly FileStream instanceLock;
    private List<ScheduledMessage> items = new();
    public IReadOnlyList<ScheduledMessage> Items => items.AsReadOnly();

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
            items = JsonSerializer.Deserialize<List<ScheduledMessage>>(plaintext)
                ?? throw new InvalidDataException("Invalid queue");
            items = items.Select(message => message.State == MessageStatus.Sending
                ? message with { State = MessageStatus.Unknown } : message).ToList();
            Save();
        }
    }

    public void Add(ScheduledMessage message)
    {
        items.Add(message);
        Save();
    }

    public void Remove(Guid id)
    {
        items.RemoveAll(message => message.Id == id);
        Save();
    }

    public void ChangeStatus(Guid id, MessageStatus state)
    {
        items = items.Select(message => message.Id == id ? message with { State = state } : message).ToList();
        Save();
    }

    public bool TryEditPendingText(Guid id, string text)
    {
        var message = items.SingleOrDefault(item => item.Id == id);
        if (message == null || message.State != MessageStatus.Pending ||
            (string.IsNullOrWhiteSpace(text) && (message.Attachments?.Count ?? 0) == 0)) return false;
        var updated = items.Select(item => item.Id == id ? item with { Text = text } : item).ToList();
        Save(updated);
        items = updated;
        return true;
    }

    private void Save(IEnumerable<ScheduledMessage>? contents = null)
    {
        var bytes = cipher.Encrypt(JsonSerializer.SerializeToUtf8Bytes(contents ?? items));
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
