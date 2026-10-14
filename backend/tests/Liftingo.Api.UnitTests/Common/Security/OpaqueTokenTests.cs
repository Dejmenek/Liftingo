using Liftingo.Application.Common.Security;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Liftingo.Api.UnitTests.Common.Security;

public class OpaqueTokenTests
{
    [Fact]
    public void Generate_Always_ReturnsBase64UrlOf32Bytes()
    {
        (string token, _) = OpaqueToken.Generate();

        byte[] decoded = Base64Url.DecodeFromChars(token);
        Assert.Equal(32, decoded.Length);
        Assert.DoesNotContain("=", token, StringComparison.Ordinal);
        Assert.DoesNotContain("+", token, StringComparison.Ordinal);
        Assert.DoesNotContain("/", token, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_Always_ReturnsSha256OfTheTokenBytes()
    {
        (string token, byte[] hash) = OpaqueToken.Generate();

        Assert.Equal(SHA256.HashData(Base64Url.DecodeFromChars(token)), hash);
    }

    [Fact]
    public void Generate_CalledTwice_ReturnsDifferentTokens()
    {
        (string first, _) = OpaqueToken.Generate();
        (string second, _) = OpaqueToken.Generate();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Hash_GeneratedToken_MatchesTheGeneratedHash()
    {
        (string token, byte[] hash) = OpaqueToken.Generate();

        Assert.Equal(hash, OpaqueToken.Hash(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64url!")]
    [InlineData("abc+def/ghi=")]
    [InlineData("a")]
    public void Hash_InputThatIsNotBase64Url_ReturnsNull(string token)
    {
        Assert.Null(OpaqueToken.Hash(token));
    }

    [Fact]
    public void Hash_ValidBase64Url_ReturnsSha256OfTheDecodedBytes()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("some opaque bytes");
        string token = Base64Url.EncodeToString(bytes);

        Assert.Equal(SHA256.HashData(bytes), OpaqueToken.Hash(token));
    }
}