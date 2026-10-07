using System.Security.Cryptography;

namespace SignalScheduler.Security;

public sealed class QueueCipher(byte[] key) : IDisposable
{
    // Legacy envelope: 12-byte nonce, 16-byte authentication tag, ciphertext.
    public byte[] Encrypt(byte[] plaintext)
    {
        var bytes = new byte[28 + plaintext.Length];
        RandomNumberGenerator.Fill(bytes.AsSpan(0, 12));
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(bytes.AsSpan(0, 12), plaintext, bytes.AsSpan(28), bytes.AsSpan(12, 16));
        return bytes;
    }

    public byte[] Decrypt(byte[] bytes)
    {
        if (bytes.Length < 28)
            throw new InvalidDataException("Queue is damaged. Original file was preserved.");
        var plaintext = new byte[bytes.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plaintext);
        return plaintext;
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(key);
}
