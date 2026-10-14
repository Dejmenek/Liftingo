using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace Liftingo.Infrastructure.Sessions;

/// <summary>
/// <see cref="ILoginThrottle"/> over the session store's Redis. One hash per address,
/// <c>liftingo:login-throttle:{hex SHA-256 of the trimmed, upper-cased address}</c>, with <c>failures</c> and
/// <c>lockoutEnd</c> (Unix milliseconds, so the script can compare it). The address itself is never stored.
/// </summary>
/// <remarks>
/// The current time comes from <see cref="TimeProvider"/>, not from Redis, so a fake clock in tests moves the
/// lockout too. Redis only expires an entry once its lockout has ended and its counter is zero.
/// </remarks>
public sealed class RedisLoginThrottle(
    IConnectionMultiplexer redis,
    IOptions<LoginLockoutOptions> lockoutOptions,
    TimeProvider timeProvider) : ILoginThrottle
{
    private const string KeyPrefix = "liftingo:login-throttle:";

    // Script results other than a lockout end.
    private const long AlreadyLockedOut = -1;
    private const long Counted = 0;

    private static readonly string s_failScript = RedisCalls.LoadScript("login-throttle-fail.lua");

    public async Task<DateTimeOffset?> GetLockoutEndAsync(string email, CancellationToken ct)
    {
        return await RedisCalls.GuardAsync<DateTimeOffset?>(async () =>
        {
            ct.ThrowIfCancellationRequested();

            RedisValue lockoutEnd = await redis.GetDatabase().HashGetAsync(KeyFor(email), "lockoutEnd");

            if (!lockoutEnd.TryParse(out long endMilliseconds))
            {
                return null;
            }

            DateTimeOffset end = DateTimeOffset.FromUnixTimeMilliseconds(endMilliseconds);

            return end > timeProvider.GetUtcNow() ? end : null;
        }, ct);
    }

    public async Task<PasswordCheckResult.LockoutTriggered?> RegisterFailureAsync(string email, CancellationToken ct)
    {
        LoginLockoutOptions lockout = lockoutOptions.Value;

        return await RedisCalls.GuardAsync<PasswordCheckResult.LockoutTriggered?>(async () =>
        {
            ct.ThrowIfCancellationRequested();

            RedisResult result = await redis.GetDatabase().ScriptEvaluateAsync(
                s_failScript,
                [KeyFor(email)],
                [
                    timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                    lockout.MaxFailedAccessAttempts,
                    (long)lockout.Duration.TotalMilliseconds,
                ]);

            long outcome = (long)result;

            return outcome is AlreadyLockedOut or Counted
                ? null
                : new PasswordCheckResult.LockoutTriggered(DateTimeOffset.FromUnixTimeMilliseconds(outcome));
        }, ct);
    }

    internal static RedisKey KeyFor(string email) =>
        KeyPrefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())));
}