using Google.Apis.Auth;

using Liftingo.Application.Common.Security;
using Liftingo.Infrastructure.Identity.ExternalProviders;

namespace Liftingo.Api.UnitTests.Infrastructure.Identity.ExternalProviders;

public class GoogleIdTokenVerifierTests
{
    [Fact]
    public void CreateValidationSettings_ClientId_IsTheOnlyAudience()
    {
        GoogleJsonWebSignature.ValidationSettings settings =
            GoogleIdTokenVerifier.CreateValidationSettings("1234-abc.apps.googleusercontent.com");

        Assert.Equal(["1234-abc.apps.googleusercontent.com"], settings.Audience);
    }

    [Fact]
    public void ToIdentity_VerifiedEmail_KeepsTheTrimmedEmail()
    {
        var payload = new GoogleJsonWebSignature.Payload
        {
            Subject = "google-sub-1",
            Email = " anna@example.pl ",
            EmailVerified = true,
            Name = "Anna Nowak",
        };

        ExternalIdentity identity = GoogleIdTokenVerifier.ToIdentity(payload);

        Assert.Equal("Google", identity.Provider);
        Assert.Equal("google-sub-1", identity.ProviderKey);
        Assert.Equal("anna@example.pl", identity.Email);
        Assert.Equal("Anna Nowak", identity.DisplayName);
    }

    [Fact]
    public void ToIdentity_UnverifiedEmail_TreatsItAsNoEmail()
    {
        var payload = new GoogleJsonWebSignature.Payload
        {
            Subject = "google-sub-1",
            Email = "anna@example.pl",
            EmailVerified = false,
        };

        ExternalIdentity identity = GoogleIdTokenVerifier.ToIdentity(payload);

        Assert.Null(identity.Email);
        Assert.Equal("google-sub-1", identity.ProviderKey);
    }
}
