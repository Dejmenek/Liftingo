using System.Buffers.Text;
using System.Security.Cryptography;

namespace Liftingo.Application.Common.Security;

/// <summary>Random 256-bit tokens (refresh, e-mail links). Only the SHA-256 hash of a token is ever stored.</summary>
public static class OpaqueToken
{
    private const int TokenBytes = 32;

    public static (string Token, byte[] Hash) Generate()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(TokenBytes);

        return (Base64Url.EncodeToString(bytes), SHA256.HashData(bytes));
    }

    /// <returns>The SHA-256 of the token's bytes, or <c>null</c> when <paramref name="token"/> is not valid Base64Url.</returns>
    public static byte[]? Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        // A token is Base64Url of at most 32 bytes' worth of text; the buffer is always large enough.
        byte[] buffer = new byte[Base64Url.GetMaxDecodedLength(token.Length)];

        try
        {
            return Base64Url.TryDecodeFromChars(token, buffer, out int written) && written > 0
                ? SHA256.HashData(buffer.AsSpan(0, written))
                : null;
        }
        catch (FormatException)
        {
            // TryDecodeFromChars still throws on illegal characters.
            return null;
        }
    }
}