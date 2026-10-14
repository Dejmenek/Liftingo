using Liftingo.Infrastructure.Identity;

using Microsoft.Extensions.Options;

namespace Liftingo.Api.UnitTests.Common;

public class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _sut = new();

    [Fact]
    public void Validate_ValidOptions_Succeeds()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_DefaultOptions_FailsForEveryValue()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, new JwtOptions());

        Assert.Equal(4, result.Failures!.Count());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_BlankIssuer_FailsWithConfigPath(string issuer)
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions(issuer: issuer));

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Jwt:Issuer", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_BlankAudience_FailsWithConfigPath(string audience)
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions(audience: audience));

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Jwt:Audience", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SigningKeyOfExactly32Bytes_Succeeds()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions(signingKey: Key(32)));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_SigningKeyOf31Bytes_FailsWithConfigPath()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions(signingKey: Key(31)));

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Jwt:SigningKey", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SigningKeyNotBase64_FailsWithConfigPath()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions(signingKey: "this is not base64!"));

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Jwt:SigningKey", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_MissingSigningKey_FailsWithoutEchoingAnyValue()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions(signingKey: string.Empty));

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Jwt:SigningKey", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_NonPositiveLifetime_FailsWithConfigPath(int minutes)
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions(lifetime: TimeSpan.FromMinutes(minutes)));

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Jwt:AccessTokenLifetime", failure, StringComparison.Ordinal);
    }

    private static string Key(int bytes) => Convert.ToBase64String(new byte[bytes]);

    private static JwtOptions CreateOptions(
        string issuer = "liftingo-api",
        string audience = "liftingo-app",
        string? signingKey = null,
        TimeSpan? lifetime = null) =>
        new()
        {
            Issuer = issuer,
            Audience = audience,
            SigningKey = signingKey ?? Key(32),
            AccessTokenLifetime = lifetime ?? TimeSpan.FromMinutes(15),
        };
}