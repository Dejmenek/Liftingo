using System.Net;

using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Audit;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class LogoutTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Logout_ActiveSession_Returns204ClearsCookieAndAuditsLogout()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, RefreshCookie original) = await RegisterAsync(client);
        Factory.Audit.Clear();

        using HttpResponseMessage response = await client.LogoutAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        RefreshCookie? cleared = AuthTestClient.ReadRefreshCookie(response);
        Assert.NotNull(cleared);
        Assert.True(cleared.IsDeleted);
        Assert.Equal("/api/v1/auth/session", cleared.Path);

        AuditRecord record = Assert.Single(Factory.Audit.Records);
        Assert.Equal(SecurityEventType.Logout, record.EventType);
        Assert.Equal(SecurityEventOutcome.Succeeded, record.Outcome);
        Assert.Equal(session.Account.Id, record.UserId);
        DecryptedAudit? decrypted = await CapturingAuditSink.DecryptAsync(record, Factory.Services);
        Assert.NotNull(decrypted);
        Assert.False(string.IsNullOrEmpty(decrypted.DeviceType));

        // Both the refresh cookie and the unexpired access token stop working at once (SC-003, FR-015b).
        using HttpResponseMessage refresh = await client.RefreshWithCookieAsync(original.Value);
        using HttpResponseMessage me = await client.GetMeAsync(session.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Logout_OneDevice_LeavesTheOtherDeviceSignedIn()
    {
        using AuthTestClient deviceA = Factory.CreateAuthClient();
        (AuthSessionResponse registered, _) = await RegisterAsync(deviceA);
        using AuthTestClient deviceB = Factory.CreateAuthClient();
        using HttpResponseMessage login = await deviceB.LoginAsync(registered.Account.Email, Password, Guid.NewGuid());
        AuthSessionResponse sessionB = await AuthTestClient.ReadSessionAsync(login);

        (await deviceA.LogoutAsync()).Dispose();

        using HttpResponseMessage refreshB = await deviceB.RefreshAsync();
        using HttpResponseMessage meB = await deviceB.GetMeAsync(sessionB.AccessToken);
        Assert.Equal(HttpStatusCode.OK, refreshB.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meB.StatusCode);
    }

    [Fact]
    public async Task Logout_AccessTokenExpiredAndOnlyCookie_StillWorks()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        using AuthTestClient client = factory.CreateAuthClient();
        (AuthSessionResponse session, RefreshCookie original) = await RegisterAsync(client);
        factory.Time.Advance(TimeSpan.FromMinutes(20));
        using (HttpResponseMessage expired = await client.GetMeAsync(session.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        }

        using HttpResponseMessage response = await client.LogoutAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(factory.Audit.Records, r => r.EventType == SecurityEventType.Logout);
        using HttpResponseMessage refresh = await client.RefreshWithCookieAsync(original.Value);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_NoCookie_Returns204WritesNoAuditRecordAndClearsCookie()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.LogoutAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(Factory.Audit.Records);
        RefreshCookie? cleared = AuthTestClient.ReadRefreshCookie(response);
        Assert.NotNull(cleared);
        Assert.True(cleared.IsDeleted);
    }

    [Fact]
    public async Task Logout_UnknownCookie_Returns204AndWritesNoAuditRecord()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.LogoutWithCookieAsync("bm90LWEtcmVhbC1yZWZyZXNoLXRva2Vu");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(Factory.Audit.Records);
    }

    [Fact]
    public async Task Logout_SameCookieTwice_IsIdempotentAndAuditedOnce()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (_, RefreshCookie original) = await RegisterAsync(client);
        Factory.Audit.Clear();

        using HttpResponseMessage first = await client.LogoutWithCookieAsync(original.Value);
        using HttpResponseMessage second = await client.LogoutWithCookieAsync(original.Value);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Single(Factory.Audit.Records, r => r.EventType == SecurityEventType.Logout);
    }

    private const string Password = "Sztanga1!";

    private static async Task<(AuthSessionResponse Session, RefreshCookie Cookie)> RegisterAsync(AuthTestClient client)
    {
        (HttpResponseMessage response, _) = await client.RegisterAsync(password: Password);
        using HttpResponseMessage owned = response;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (
            await AuthTestClient.ReadSessionAsync(response),
            AuthTestClient.ReadRefreshCookie(response) ?? throw new InvalidOperationException("No cookie on register."));
    }
}
