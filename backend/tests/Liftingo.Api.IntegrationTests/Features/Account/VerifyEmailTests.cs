using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class VerifyEmailTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task VerifyEmail_ValidToken_Returns200VerifiedAndConsumesTheToken()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, string token) = await RegisterAsync(client, Factory);

        using HttpResponseMessage response = await VerifyAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("verified", await StatusAsync(response));
        Assert.True(await EmailConfirmedAsync(session.Account.Id));
        EmailToken stored = await WithDbAsync(db => db.EmailTokens
            .SingleAsync(t => t.UserId == session.Account.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(stored.ConsumedAt);
    }

    [Fact]
    public async Task VerifyEmail_SameTokenAgain_Returns200AlreadyVerified()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (_, string token) = await RegisterAsync(client, Factory);
        (await VerifyAsync(client, token)).Dispose();

        using HttpResponseMessage response = await VerifyAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alreadyVerified", await StatusAsync(response));
    }

    [Fact]
    public async Task VerifyEmail_TokenOlderThan24HoursOfUnverifiedAccount_Returns400LinkInvalid()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        using AuthTestClient client = factory.CreateAuthClient();
        (AuthSessionResponse session, string token) = await RegisterAsync(client, factory);
        factory.Time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));

        using HttpResponseMessage response = await VerifyAsync(client, token);

        await AssertLinkInvalidAsync(response);
        Assert.False(await EmailConfirmedAsync(session.Account.Id));
    }

    [Fact]
    public async Task VerifyEmail_SupersededTokenOfUnverifiedAccount_Returns400LinkInvalid()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, string firstToken) = await RegisterAsync(client, Factory);
        (await client.PostJsonAsync(RouteConsts.Auth.ResendVerificationEmail, body: null, session.AccessToken)).Dispose();

        using HttpResponseMessage response = await VerifyAsync(client, firstToken);

        await AssertLinkInvalidAsync(response);
        Assert.False(await EmailConfirmedAsync(session.Account.Id));
    }

    [Theory]
    [InlineData("not a token")]
    [InlineData("%%%")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task VerifyEmail_UnknownOrMalformedToken_Returns400LinkInvalid(string token)
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await VerifyAsync(client, token);

        await AssertLinkInvalidAsync(response);
    }

    [Fact]
    public async Task VerifyEmail_EmptyToken_Returns400Validation()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await VerifyAsync(client, string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Contains(
            body.GetProperty("errors").EnumerateArray(),
            e => e.GetProperty("code").GetString() == "Validation.TokenRequired");
    }

    [Fact]
    public async Task VerifyEmail_SentWithAnotherUsersBearer_VerifiesTheTokensAccountNotTheSignedInOne()
    {
        using AuthTestClient clientA = Factory.CreateAuthClient();
        (AuthSessionResponse sessionA, string tokenA) = await RegisterAsync(clientA, Factory);
        using AuthTestClient clientB = Factory.CreateAuthClient();
        (AuthSessionResponse sessionB, _) = await RegisterAsync(clientB, Factory);

        using HttpResponseMessage response = await clientB.PostJsonAsync(
            RouteConsts.Auth.VerifyEmail,
            new { token = tokenA },
            sessionB.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await EmailConfirmedAsync(sessionA.Account.Id));
        Assert.False(await EmailConfirmedAsync(sessionB.Account.Id));
    }

    [Fact]
    public async Task VerifyEmail_AfterRefresh_NewAccessTokenCarriesEmailVerifiedTrue()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, string token) = await RegisterAsync(client, Factory);
        Assert.False(EmailVerifiedClaim(session.AccessToken));
        (await VerifyAsync(client, token)).Dispose();

        using HttpResponseMessage refresh = await client.RefreshAsync();

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        AuthSessionResponse renewed = await AuthTestClient.ReadSessionAsync(refresh);
        Assert.True(EmailVerifiedClaim(renewed.AccessToken));
        Assert.True(renewed.Account.EmailVerified);
    }

    [Fact]
    public async Task VerifyEmail_AccountVerifiedElsewhereWithTokenSuperseded_Returns200AlreadyVerified()
    {
        // A provider takeover or a password reset verifies the e-mail and supersedes the open verification tokens.
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, string token) = await RegisterAsync(client, Factory);
        await WithDbAsync(async db =>
        {
            ApplicationUser user = await db.Users.SingleAsync(u => u.Id == session.Account.Id, TestContext.Current.CancellationToken);
            user.EmailConfirmed = true;
            EmailToken stored = await db.EmailTokens.SingleAsync(t => t.UserId == user.Id, TestContext.Current.CancellationToken);
            stored.Supersede(Factory.Time.GetUtcNow());
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            return 0;
        });

        using HttpResponseMessage response = await VerifyAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alreadyVerified", await StatusAsync(response));
    }

    /// <summary>Registers an account and returns the session and the token from its (then cleared) welcome e-mail.</summary>
    internal static async Task<(AuthSessionResponse Session, string Token)> RegisterAsync(AuthTestClient client, ApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(factory);

        (HttpResponseMessage response, _) = await client.RegisterAsync();
        using HttpResponseMessage owned = response;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);

        string token = AuthTestClient.ExtractToken(Assert.Single(factory.Emails.Messages));
        factory.Emails.Clear();

        return (session, token);
    }

    private static Task<HttpResponseMessage> VerifyAsync(AuthTestClient client, string token) =>
        client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token });

    private static async Task<string?> StatusAsync(HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return body.GetProperty("status").GetString();
    }

    private static async Task AssertLinkInvalidAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Auth.VerificationLinkInvalid", body.GetProperty("code").GetString());
    }

    private Task<bool> EmailConfirmedAsync(Guid userId) =>
        WithDbAsync(db => db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.EmailConfirmed)
            .SingleAsync(TestContext.Current.CancellationToken));

    private static bool EmailVerifiedClaim(string jwt)
    {
        string payload = jwt.Split('.')[1];
        using JsonDocument document = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(payload)));
        JsonElement claim = document.RootElement.GetProperty("email_verified");

        return claim.ValueKind == JsonValueKind.True
            || (claim.ValueKind == JsonValueKind.String && string.Equals(claim.GetString(), "true", StringComparison.OrdinalIgnoreCase));
    }
}
