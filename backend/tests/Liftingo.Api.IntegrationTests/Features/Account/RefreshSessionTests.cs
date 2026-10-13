using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Audit;

using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

using StackExchange.Redis;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public sealed class RefreshSessionTests : IntegrationTestBase, IDisposable
{
    private static readonly TimeSpan s_inactivity = TimeSpan.FromDays(30);
    private static readonly TimeSpan s_tolerance = TimeSpan.FromSeconds(60);

    private readonly ConnectionMultiplexer _redis;

    public RefreshSessionTests(SqlServerFixture fixture)
        : base(fixture)
    {
        _redis = ConnectionMultiplexer.Connect(fixture.RedisConnectionString);
    }

    public void Dispose() => _redis.Dispose();

    [Fact]
    public async Task Refresh_ValidCookie_Returns200WithNewTokenRotatedCookieAndFullTtl()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse registered, RefreshCookie original) = await RegisterAsync(client);

        using HttpResponseMessage response = await client.RefreshAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthSessionResponse refreshed = await AuthTestClient.ReadSessionAsync(response);
        Assert.NotEqual(registered.AccessToken, refreshed.AccessToken);
        Assert.Equal(registered.Account.Id, refreshed.Account.Id);
        Assert.Equal(registered.Account.Email, refreshed.Account.Email);
        Assert.Equal(SessionId(registered.AccessToken), SessionId(refreshed.AccessToken));

        RefreshCookie? rotated = AuthTestClient.ReadRefreshCookie(response);
        Assert.NotNull(rotated);
        Assert.NotEqual(original.Value, rotated.Value);
        Assert.True(rotated.HttpOnly);
        Assert.True(rotated.Secure);
        Assert.Equal("Lax", rotated.SameSite, ignoreCase: true);
        Assert.Equal("/api/v1/auth/session", rotated.Path);
        Assert.Equal(s_inactivity.TotalSeconds, rotated.MaxAgeSeconds ?? 0, 5);

        TimeSpan? ttl = await _redis.GetDatabase().KeyTimeToLiveAsync(SessionKey(refreshed.AccessToken));
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, s_inactivity - s_tolerance, s_inactivity);

        using HttpResponseMessage me = await client.GetMeAsync(refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        // The rotated cookie works again.
        using HttpResponseMessage second = await client.RefreshAsync();
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReflectsCurrentEmailVerifiedState()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse registered, _) = await RegisterAsync(client);
        await WithDbAsync(db => db.Users
            .Where(u => u.Id == registered.Account.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmed, true), TestContext.Current.CancellationToken));

        using HttpResponseMessage response = await client.RefreshAsync();

        AuthSessionResponse refreshed = await AuthTestClient.ReadSessionAsync(response);
        Assert.True(refreshed.Account.EmailVerified);
        Assert.Equal("true", new JsonWebToken(refreshed.AccessToken).GetClaim("email_verified").Value);
    }

    [Fact]
    public async Task Refresh_NoCookie_Returns401SessionInvalidAndClearsCookie()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.RefreshAsync();

        await AssertSessionInvalidAsync(response);
    }

    [Fact]
    public async Task Refresh_UnknownCookie_Returns401SessionInvalidAndClearsCookie()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.RefreshWithCookieAsync("bm90LWEtcmVhbC1yZWZyZXNoLXRva2Vu");

        await AssertSessionInvalidAsync(response);
    }

    [Fact]
    public async Task Refresh_CookieThatIsNotBase64Url_Returns401SessionInvalid()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.RefreshWithCookieAsync("***not base64***");

        await AssertSessionInvalidAsync(response);
    }

    [Fact]
    public async Task Refresh_SessionKeyDeleted_Returns401SessionInvalidAndClearsCookie()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse registered, _) = await RegisterAsync(client);
        await _redis.GetDatabase().KeyDeleteAsync(SessionKey(registered.AccessToken));

        using HttpResponseMessage response = await client.RefreshAsync();

        await AssertSessionInvalidAsync(response);
    }

    [Fact]
    public async Task Refresh_ReplayedPreviousCookie_RevokesTheSessionAndAuditsReuse()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse registered, RefreshCookie original) = await RegisterAsync(client);
        using HttpResponseMessage rotated = await client.RefreshAsync();
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        AuthSessionResponse refreshed = await AuthTestClient.ReadSessionAsync(rotated);
        Factory.Audit.Clear();

        using HttpResponseMessage replay = await client.RefreshWithCookieAsync(original.Value);

        await AssertSessionInvalidAsync(replay);

        // The whole chain is dead, including the newer cookie and both access tokens.
        using HttpResponseMessage newer = await client.RefreshAsync();
        await AssertSessionInvalidAsync(newer);
        using HttpResponseMessage meOld = await client.GetMeAsync(registered.AccessToken);
        using HttpResponseMessage meNew = await client.GetMeAsync(refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, meOld.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meNew.StatusCode);

        AuditRecord record = Assert.Single(Factory.Audit.Records);
        Assert.Equal(SecurityEventType.RefreshTokenReuse, record.EventType);
        Assert.Equal(registered.Account.Id, record.UserId);
        DecryptedAudit? decrypted = await CapturingAuditSink.DecryptAsync(record, Factory.Services);
        Assert.NotNull(decrypted);
        Assert.False(string.IsNullOrEmpty(decrypted.DeviceType));
    }

    [Fact]
    public async Task Refresh_ReuseOnOneDevice_LeavesAnotherDeviceSessionAlone()
    {
        using AuthTestClient deviceA = Factory.CreateAuthClient();
        (AuthSessionResponse registered, RefreshCookie original) = await RegisterAsync(deviceA);
        using AuthTestClient deviceB = Factory.CreateAuthClient();
        using HttpResponseMessage login = await deviceB.LoginAsync(
            registered.Account.Email,
            "Sztanga1!",
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        (await deviceA.RefreshAsync()).Dispose();
        (await deviceA.RefreshWithCookieAsync(original.Value)).Dispose();

        using HttpResponseMessage otherDevice = await deviceB.RefreshAsync();

        Assert.Equal(HttpStatusCode.OK, otherDevice.StatusCode);
    }

    [Fact]
    public async Task Refresh_RoutineRenewal_WritesNoAuditRecord()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        await RegisterAsync(client);
        Factory.Audit.Clear();

        (await client.RefreshAsync()).Dispose();
        (await client.RefreshAsync()).Dispose();

        Assert.Empty(Factory.Audit.Records);
    }

    [Fact]
    public async Task Refresh_NearTheAbsoluteLifetime_CapsTtlAndCookieMaxAgeThenRefusesAfterTheCap()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        using AuthTestClient client = factory.CreateAuthClient();
        (AuthSessionResponse registered, _) = await RegisterAsync(client);
        string sessionKey = SessionKey(registered.AccessToken);

        // Renewing every 29 days never lets the 30-day inactivity window lapse.
        await RefreshAfterAsync(factory, client, TimeSpan.FromDays(29));
        await RefreshAfterAsync(factory, client, TimeSpan.FromDays(29));

        // Day 87: 3 days left of the 90.
        RefreshCookie capped = await RefreshAfterAsync(factory, client, TimeSpan.FromDays(29));
        Assert.Equal(TimeSpan.FromDays(3).TotalSeconds, capped.MaxAgeSeconds ?? 0, 5);
        TimeSpan? ttl = await _redis.GetDatabase().KeyTimeToLiveAsync(sessionKey);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromDays(3) - s_tolerance, TimeSpan.FromDays(3));

        // Daily-style renewals keep shrinking the remainder; they never extend the cap.
        RefreshCookie lastDay = await RefreshAfterAsync(factory, client, TimeSpan.FromDays(2));
        Assert.Equal(TimeSpan.FromDays(1).TotalSeconds, lastDay.MaxAgeSeconds ?? 0, 5);

        factory.Time.Advance(TimeSpan.FromDays(2));
        using HttpResponseMessage pastCap = await client.RefreshAsync();

        await AssertSessionInvalidAsync(pastCap);
    }

    private static async Task<RefreshCookie> RefreshAfterAsync(ApiFactory factory, AuthTestClient client, TimeSpan wait)
    {
        factory.Time.Advance(wait);

        using HttpResponseMessage response = await client.RefreshAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return AuthTestClient.ReadRefreshCookie(response) ?? throw new InvalidOperationException("No cookie on refresh.");
    }

    private static async Task<(AuthSessionResponse Session, RefreshCookie Cookie)> RegisterAsync(AuthTestClient client)
    {
        (HttpResponseMessage response, _) = await client.RegisterAsync(password: "Sztanga1!");
        using HttpResponseMessage owned = response;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (
            await AuthTestClient.ReadSessionAsync(response),
            AuthTestClient.ReadRefreshCookie(response) ?? throw new InvalidOperationException("No cookie on register."));
    }

    private static async Task AssertSessionInvalidAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Auth.SessionInvalid", body.GetProperty("code").GetString());

        RefreshCookie? cookie = AuthTestClient.ReadRefreshCookie(response);
        Assert.NotNull(cookie);
        Assert.True(cookie.IsDeleted);
        Assert.Equal("/api/v1/auth/session", cookie.Path);
    }

    private static string SessionId(string accessToken) => new JsonWebToken(accessToken).GetClaim("sid").Value;

    private static string SessionKey(string accessToken) => "liftingo:session:" + SessionId(accessToken);
}
