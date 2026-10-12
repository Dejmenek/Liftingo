using System.Security.Cryptography;
using System.Text;

namespace Liftingo.Infrastructure.Audit;

/// <summary>AES-256-GCM with a fresh 12-byte nonce per value. The output is Base64 of nonce + ciphertext + tag.</summary>
internal static class AuditCipher
{
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    public static string Encrypt(byte[] key, string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        byte[] output = new byte[NonceBytes + data.Length + TagBytes];

        Span<byte> nonce = output.AsSpan(0, NonceBytes);
        Span<byte> ciphertext = output.AsSpan(NonceBytes, data.Length);
        Span<byte> tag = output.AsSpan(NonceBytes + data.Length, TagBytes);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, data, ciphertext, tag);

        return Convert.ToBase64String(output);
    }

    public static string Decrypt(byte[] key, string cipher)
    {
        if (string.IsNullOrEmpty(cipher))
        {
            return string.Empty;
        }

        byte[] input = Convert.FromBase64String(cipher);
        int dataLength = input.Length - NonceBytes - TagBytes;
        byte[] plaintext = new byte[dataLength];

        using var aes = new AesGcm(key, TagBytes);
        aes.Decrypt(
            input.AsSpan(0, NonceBytes),
            input.AsSpan(NonceBytes, dataLength),
            input.AsSpan(NonceBytes + dataLength, TagBytes),
            plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
