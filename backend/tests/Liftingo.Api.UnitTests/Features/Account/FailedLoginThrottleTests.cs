using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Liftingo.Api.UnitTests.Features.Account;

public sealed class FailedLoginThrottleTests : IDisposable
{
    private const string Email = "anna@example.pl";

    private static readonly TimeSpan s_lockoutDuration = TimeSpan.FromMinutes(15);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly FailedLoginThrottle _throttle;

    public FailedLoginThrottleTests()
    {
        var lockoutOptions = new LoginLockoutOptions { MaxFailedAccessAttempts = 5, Duration = s_lockoutDuration };

        _throttle = new FailedLoginThrottle(_cache, _time, Options.Create(lockoutOptions));
    }

    public void Dispose()
    {
        _cache.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RegisterFailure_FourFailures_DoesNotLock()
    {
        for (int i = 0; i < 4; i++)
        {
            Assert.Null(_throttle.RegisterFailure(Email));
        }

        Assert.False(_throttle.IsLockedOut(Email, out _));
    }

    [Fact]
    public void RegisterFailure_FifthFailure_LocksForFifteenMinutes()
    {
        FailFourTimes();

        PasswordCheckResult.LockoutTriggered? triggered = _throttle.RegisterFailure(Email);

        Assert.NotNull(triggered);
        Assert.Equal(_time.GetUtcNow() + s_lockoutDuration, triggered.RetryAfter);
        Assert.True(_throttle.IsLockedOut(Email, out DateTimeOffset retryAfter));
        Assert.Equal(triggered.RetryAfter, retryAfter);
    }

    [Fact]
    public void RegisterFailure_DuringLockout_IsRefusedAndDoesNotExtendIt()
    {
        FailFourTimes();
        PasswordCheckResult.LockoutTriggered original = _throttle.RegisterFailure(Email)!;
        _time.Advance(TimeSpan.FromMinutes(5));

        PasswordCheckResult.LockoutTriggered? duringLockout = _throttle.RegisterFailure(Email);

        Assert.Null(duringLockout);
        Assert.True(_throttle.IsLockedOut(Email, out DateTimeOffset retryAfter));
        Assert.Equal(original.RetryAfter, retryAfter);
    }

    [Fact]
    public void IsLockedOut_AfterLockoutDuration_IsFalse()
    {
        FailFourTimes();
        _throttle.RegisterFailure(Email);

        _time.Advance(s_lockoutDuration + TimeSpan.FromSeconds(1));

        Assert.False(_throttle.IsLockedOut(Email, out _));
    }

    [Fact]
    public void RegisterFailure_AfterLockoutTriggered_CounterStartsAgainFromZero()
    {
        FailFourTimes();
        _throttle.RegisterFailure(Email);
        _time.Advance(s_lockoutDuration + TimeSpan.FromSeconds(1));

        // Four more failures do not lock; only the fifth does.
        for (int i = 0; i < 4; i++)
        {
            Assert.Null(_throttle.RegisterFailure(Email));
        }

        Assert.False(_throttle.IsLockedOut(Email, out _));
        Assert.NotNull(_throttle.RegisterFailure(Email));
    }

    [Fact]
    public void RegisterFailure_FailuresSpreadOverHours_StillCountTowardsLockout()
    {
        for (int i = 0; i < 4; i++)
        {
            _throttle.RegisterFailure(Email);
            _time.Advance(TimeSpan.FromHours(2));
        }

        Assert.NotNull(_throttle.RegisterFailure(Email));
    }

    [Fact]
    public void RegisterFailure_EmailInDifferentCaseOrWithWhitespace_SharesOneCounter()
    {
        _throttle.RegisterFailure(Email);
        _throttle.RegisterFailure("ANNA@EXAMPLE.PL");
        _throttle.RegisterFailure("  anna@example.pl  ");
        _throttle.RegisterFailure(Email);

        Assert.NotNull(_throttle.RegisterFailure("Anna@Example.pl"));
    }

    [Fact]
    public void RegisterFailure_DifferentEmails_AreCountedSeparately()
    {
        FailFourTimes();

        Assert.Null(_throttle.RegisterFailure("ola@example.pl"));
        Assert.False(_throttle.IsLockedOut("ola@example.pl", out _));
    }

    [Fact]
    public void RegisterFailure_RepeatedFailuresForOneEmail_UseOneCacheEntryKeyedByHash()
    {
        _throttle.RegisterFailure(Email);
        _throttle.RegisterFailure(Email);

        Assert.Equal(1, _cache.Count);
        Assert.DoesNotContain(
            _cache.Keys,
            key => key.ToString()!.Contains("anna", StringComparison.OrdinalIgnoreCase));
    }

    private void FailFourTimes()
    {
        for (int i = 0; i < 4; i++)
        {
            _throttle.RegisterFailure(Email);
        }
    }
}
