using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Infrastructure.Sessions;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using StackExchange.Redis;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

// Runs the real Lua script against the Redis container. The throttle gets its own fake clock, so the
// lockout can run out without waiting. Redis' own TTL follows the real clock, which is never behind the fake one.
public sealed class RedisLoginThrottleTests : IntegrationTestBase, IDisposable
{
    private const string Email = "nikt@example.pl";
    private const int MaxFailures = 5;

    private static readonly TimeSpan s_lockout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan s_ttlTolerance = TimeSpan.FromSeconds(30);

    private readonly ConnectionMultiplexer _redis;
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly RedisLoginThrottle _throttle;

    public RedisLoginThrottleTests(SqlServerFixture fixture)
        : base(fixture)
    {
        _redis = ConnectionMultiplexer.Connect(fixture.RedisConnectionString);

        var options = new LoginLockoutOptions { MaxFailedAccessAttempts = MaxFailures, Duration = s_lockout };
        _throttle = new RedisLoginThrottle(_redis, Options.Create(options), _time);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RegisterFailureAsync_FourFailures_DoesNotLockAndKeepsTheCounterWithoutTtl()
    {
        await FailAsync(4);

        Assert.Null(await _throttle.GetLockoutEndAsync(Email, Ct));
        Assert.Equal(4, (int)await Database.HashGetAsync(Key, "failures"));
        Assert.Null(await Database.KeyTimeToLiveAsync(Key));
    }

    [Fact]
    public async Task RegisterFailureAsync_FifthFailure_LocksResetsTheCounterAndExpiresWithTheLockout()
    {
        await FailAsync(4);

        PasswordCheckResult.LockoutTriggered? triggered = await _throttle.RegisterFailureAsync(Email, Ct);

        Assert.NotNull(triggered);
        Assert.Equal(TruncatedToMilliseconds(_time.GetUtcNow() + s_lockout), triggered.RetryAfter);
        Assert.Equal(triggered.RetryAfter, await _throttle.GetLockoutEndAsync(Email, Ct));
        Assert.Equal(0, (int)await Database.HashGetAsync(Key, "failures"));

        TimeSpan? ttl = await Database.KeyTimeToLiveAsync(Key);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, s_lockout - s_ttlTolerance, s_lockout);
    }

    [Fact]
    public async Task RegisterFailureAsync_DuringLockout_IsNotCountedAndDoesNotExtendIt()
    {
        await FailAsync(MaxFailures);
        DateTimeOffset? lockoutEnd = await _throttle.GetLockoutEndAsync(Email, Ct);

        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Null(await _throttle.RegisterFailureAsync(Email, Ct));
        Assert.Equal(lockoutEnd, await _throttle.GetLockoutEndAsync(Email, Ct));
        Assert.Equal(0, (int)await Database.HashGetAsync(Key, "failures"));
    }

    [Fact]
    public async Task RegisterFailureAsync_AfterTheLockoutEnds_CountsFromZeroAgain()
    {
        await FailAsync(MaxFailures);

        _time.Advance(s_lockout + TimeSpan.FromSeconds(1));

        Assert.Null(await _throttle.GetLockoutEndAsync(Email, Ct));
        await FailAsync(MaxFailures - 1);
        Assert.Null(await _throttle.GetLockoutEndAsync(Email, Ct));
        Assert.NotNull(await _throttle.RegisterFailureAsync(Email, Ct));
    }

    [Fact]
    public async Task RegisterFailureAsync_CounterDoesNotDecay_LocksOnTheFifthFailureMuchLater()
    {
        await FailAsync(MaxFailures - 1);

        _time.Advance(TimeSpan.FromDays(30));

        Assert.NotNull(await _throttle.RegisterFailureAsync(Email, Ct));
    }

    [Fact]
    public async Task RegisterFailureAsync_OtherSpellingOfTheAddress_CountsInTheSameEntry()
    {
        await FailAsync(MaxFailures - 1);

        Assert.NotNull(await _throttle.RegisterFailureAsync($"  {Email.ToUpperInvariant()} ", Ct));
        Assert.NotNull(await _throttle.GetLockoutEndAsync(Email, Ct));
    }

    [Fact]
    public async Task RegisterFailureAsync_AnyAddress_StoresAHashAndNeverTheAddress()
    {
        await FailAsync(1);

        List<RedisKey> keys = [];

        await foreach (RedisKey found in _redis.GetServers().Single().KeysAsync(pattern: "liftingo:login-throttle:*"))
        {
            keys.Add(found);
        }

        RedisKey key = Assert.Single(keys);
        Assert.DoesNotContain("nikt", key.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Matches("^liftingo:login-throttle:[0-9a-f]{64}$", key.ToString());

        HashEntry[] fields = await Database.HashGetAllAsync(key);
        Assert.DoesNotContain(fields, f => f.Value.ToString().Contains("nikt", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _redis.Dispose();

    private IDatabase Database => _redis.GetDatabase();

    private static RedisKey Key => RedisLoginThrottle.KeyFor(Email);

    // The lockout end is stored in Unix milliseconds.
    private static DateTimeOffset TruncatedToMilliseconds(DateTimeOffset value) =>
        DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());

    private async Task FailAsync(int times)
    {
        for (int i = 0; i < times; i++)
        {
            await _throttle.RegisterFailureAsync(Email, Ct);
        }
    }
}