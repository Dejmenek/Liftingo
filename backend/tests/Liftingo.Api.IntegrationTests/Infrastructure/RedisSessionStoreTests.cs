using System.Buffers.Text;

using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Infrastructure.Sessions;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using StackExchange.Redis;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

// Runs the real Lua scripts against the Redis container. The store gets its own fake clock and options,
// so the 30-day and 90-day windows can be crossed without waiting.
public sealed class RedisSessionStoreTests : IntegrationTestBase, IDisposable
{
    private static readonly TimeSpan s_inactivity = TimeSpan.FromDays(30);
    private static readonly TimeSpan s_absolute = TimeSpan.FromDays(90);
    private static readonly TimeSpan s_ttlTolerance = TimeSpan.FromSeconds(30);

    private readonly ConnectionMultiplexer _redis;
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly RedisSessionStore _store;
    private readonly SessionDevice _device = new(Guid.NewGuid(), "Chrome, Windows, komputer");

    public RedisSessionStoreTests(SqlServerFixture fixture)
        : base(fixture)
    {
        _redis = ConnectionMultiplexer.Connect(fixture.RedisConnectionString);

        var options = new SessionsOptions { InactivityTimeout = s_inactivity, AbsoluteLifetime = s_absolute };
        _store = new RedisSessionStore(_redis, Options.Create(options), _time);
    }

    [Fact]
    public async Task CreateAsync_NewSession_StoresSessionTokenAndUserSetWithTtls()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();

        (Guid sessionId, string refreshToken, TimeSpan remaining) = await _store.CreateAsync(userId, _device, ct);

        IDatabase db = _redis.GetDatabase();
        HashEntry[] session = await db.HashGetAllAsync(SessionKey(sessionId));
        Assert.Equal(userId.ToString(), Field(session, "userId"));
        Assert.Equal(_device.DeviceId.ToString(), Field(session, "deviceId"));
        Assert.Equal(_device.DeviceType, Field(session, "deviceType"));
        Assert.Equal(TokenHash(refreshToken), Field(session, "currentTokenHash"));
        Assert.Equal(_time.GetUtcNow().Add(s_absolute).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture), Field(session, "absoluteExpiresAt"));
        Assert.Equal("active", await db.HashGetAsync(TokenKey(refreshToken), "state"));
        Assert.Equal(sessionId.ToString(), await db.HashGetAsync(TokenKey(refreshToken), "sessionId"));
        Assert.True(await db.SetContainsAsync(UserKey(userId), sessionId.ToString()));
        Assert.Equal(s_inactivity, remaining);
        AssertTtlNear(s_inactivity, await db.KeyTimeToLiveAsync(SessionKey(sessionId)));
        AssertTtlNear(s_inactivity, await db.KeyTimeToLiveAsync(TokenKey(refreshToken)));
        AssertTtlNear(s_inactivity, await db.KeyTimeToLiveAsync(UserKey(userId)));
        Assert.True(await _store.IsActiveAsync(sessionId, ct));
    }

    [Fact]
    public async Task RotateAsync_CurrentToken_ReturnsNewTokenAndMarksTheOldOneUsed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        (Guid sessionId, string oldToken, _) = await _store.CreateAsync(userId, _device, ct);

        RotationResult result = await _store.RotateAsync(Hash(oldToken), ct);

        var rotated = Assert.IsType<RotationResult.Rotated>(result);
        Assert.Equal(new SessionInfo(sessionId, userId, _device), rotated.Session);
        Assert.NotEqual(oldToken, rotated.NewRefreshToken);
        Assert.Equal(s_inactivity, rotated.RemainingLifetime);
        IDatabase db = _redis.GetDatabase();
        Assert.Equal("used", await db.HashGetAsync(TokenKey(oldToken), "state"));
        Assert.Equal("active", await db.HashGetAsync(TokenKey(rotated.NewRefreshToken), "state"));
        Assert.Equal(TokenHash(rotated.NewRefreshToken), await db.HashGetAsync(SessionKey(sessionId), "currentTokenHash"));
        AssertTtlNear(s_inactivity, await db.KeyTimeToLiveAsync(SessionKey(sessionId)));
        Assert.True(await _store.IsActiveAsync(sessionId, ct));
    }

    [Fact]
    public async Task RotateAsync_NewTokenAfterRotation_RotatesAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_, string first, _) = await _store.CreateAsync(Guid.NewGuid(), _device, ct);
        var firstRotation = Assert.IsType<RotationResult.Rotated>(await _store.RotateAsync(Hash(first), ct));

        RotationResult second = await _store.RotateAsync(Hash(firstRotation.NewRefreshToken), ct);

        Assert.IsType<RotationResult.Rotated>(second);
    }

    [Fact]
    public async Task RotateAsync_ReplayedOldToken_ReturnsReusedAndDeletesTheSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        (Guid sessionId, string oldToken, _) = await _store.CreateAsync(userId, _device, ct);
        var rotated = Assert.IsType<RotationResult.Rotated>(await _store.RotateAsync(Hash(oldToken), ct));

        RotationResult replay = await _store.RotateAsync(Hash(oldToken), ct);

        var reused = Assert.IsType<RotationResult.Reused>(replay);
        Assert.Equal(new SessionInfo(sessionId, userId, _device), reused.Session);
        Assert.False(await _store.IsActiveAsync(sessionId, ct));
        IDatabase db = _redis.GetDatabase();
        Assert.False(await db.KeyExistsAsync(TokenKey(rotated.NewRefreshToken)));
        Assert.False(await db.SetContainsAsync(UserKey(userId), sessionId.ToString()));
        Assert.IsType<RotationResult.Invalid>(await _store.RotateAsync(Hash(rotated.NewRefreshToken), ct));
    }

    [Fact]
    public async Task RotateAsync_UnknownToken_ReturnsInvalid()
    {
        RotationResult result = await _store.RotateAsync(Hash("c29tZS11bmtub3duLXRva2Vu"), TestContext.Current.CancellationToken);

        Assert.IsType<RotationResult.Invalid>(result);
    }

    [Fact]
    public async Task RotateAsync_TwoConcurrentRotationsOfOneToken_ProduceExactlyOneRotated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_, string token, _) = await _store.CreateAsync(Guid.NewGuid(), _device, ct);
        byte[] hash = Hash(token);

        RotationResult[] results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Task.Run(() => _store.RotateAsync(hash, ct), ct)));

        Assert.Single(results.OfType<RotationResult.Rotated>());
        Assert.Equal(9, results.Count(r => r is not RotationResult.Rotated));
    }

    [Fact]
    public async Task RotateAsync_NearTheAbsoluteCap_CapsTheTtl()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (Guid sessionId, string token, _) = await _store.CreateAsync(Guid.NewGuid(), _device, ct);
        TimeSpan untilCap = TimeSpan.FromHours(12);
        _time.Advance(s_absolute - untilCap);

        RotationResult result = await _store.RotateAsync(Hash(token), ct);

        var rotated = Assert.IsType<RotationResult.Rotated>(result);
        Assert.InRange(rotated.RemainingLifetime, untilCap - s_ttlTolerance, untilCap);
        IDatabase db = _redis.GetDatabase();
        AssertTtlNear(untilCap, await db.KeyTimeToLiveAsync(SessionKey(sessionId)));
        AssertTtlNear(untilCap, await db.KeyTimeToLiveAsync(TokenKey(rotated.NewRefreshToken)));
    }

    [Fact]
    public async Task RotateAsync_PastTheAbsoluteCap_ReturnsInvalidAndEndsTheSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (Guid sessionId, string token, _) = await _store.CreateAsync(Guid.NewGuid(), _device, ct);
        _time.Advance(s_absolute + TimeSpan.FromMinutes(1));

        RotationResult result = await _store.RotateAsync(Hash(token), ct);

        Assert.IsType<RotationResult.Invalid>(result);
        Assert.False(await _store.IsActiveAsync(sessionId, ct));
    }

    [Fact]
    public async Task RevokeByTokenAsync_CurrentToken_ReturnsTheSessionAndRemovesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        (Guid sessionId, string token, _) = await _store.CreateAsync(userId, _device, ct);

        SessionInfo? revoked = await _store.RevokeByTokenAsync(Hash(token), ct);

        Assert.Equal(new SessionInfo(sessionId, userId, _device), revoked);
        Assert.False(await _store.IsActiveAsync(sessionId, ct));
        Assert.IsType<RotationResult.Invalid>(await _store.RotateAsync(Hash(token), ct));
        Assert.Null(await _store.RevokeByTokenAsync(Hash(token), ct));
    }

    [Fact]
    public async Task RevokeAsync_ExistingSession_RemovesOnlyThatSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        (Guid first, _, _) = await _store.CreateAsync(userId, _device, ct);
        (Guid second, _, _) = await _store.CreateAsync(userId, _device, ct);

        await _store.RevokeAsync(first, ct);

        Assert.False(await _store.IsActiveAsync(first, ct));
        Assert.True(await _store.IsActiveAsync(second, ct));
    }

    [Fact]
    public async Task RevokeAllAsync_UserWithSeveralSessions_RemovesThemAllAndLeavesOtherUsers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var sessions = new List<(Guid SessionId, string Token)>();

        for (int i = 0; i < 3; i++)
        {
            (Guid id, string token, _) = await _store.CreateAsync(userId, _device, ct);
            sessions.Add((id, token));
        }

        (Guid otherSession, string otherToken, _) = await _store.CreateAsync(otherUserId, _device, ct);

        int revoked = await _store.RevokeAllAsync(userId, ct);

        Assert.Equal(3, revoked);

        foreach ((Guid id, string token) in sessions)
        {
            Assert.False(await _store.IsActiveAsync(id, ct));
            Assert.IsType<RotationResult.Invalid>(await _store.RotateAsync(Hash(token), ct));
        }

        Assert.False(await _redis.GetDatabase().KeyExistsAsync(UserKey(userId)));
        Assert.True(await _store.IsActiveAsync(otherSession, ct));
        Assert.IsType<RotationResult.Rotated>(await _store.RotateAsync(Hash(otherToken), ct));
    }

    [Fact]
    public async Task RevokeAllAsync_UserWithoutSessions_ReturnsZero()
    {
        int revoked = await _store.RevokeAllAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(0, revoked);
    }

    public void Dispose() => _redis.Dispose();

    private static byte[] Hash(string token) =>
        OpaqueToken.Hash(token) ?? throw new InvalidOperationException("Not a valid token.");

    private static string TokenHash(string token) => Base64Url.EncodeToString(Hash(token));

    private static RedisKey SessionKey(Guid sessionId) => "liftingo:session:" + sessionId;

    private static RedisKey TokenKey(string token) => "liftingo:rt:" + TokenHash(token);

    private static RedisKey UserKey(Guid userId) => "liftingo:user-sessions:" + userId;

    private static string? Field(HashEntry[] entries, string name) =>
        entries.Single(e => e.Name == name).Value;

    private static void AssertTtlNear(TimeSpan expected, TimeSpan? actual)
    {
        Assert.NotNull(actual);
        Assert.InRange(actual.Value, expected - s_ttlTolerance, expected);
    }
}
