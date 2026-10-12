using System.Security.Cryptography;
using System.Text;

using Liftingo.Application.Common.Security;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>
/// Applies Identity's lockout rules to e-mails that have no account, so the 6th attempt for an unknown
/// address gets the same lockout answer as one for a real account and registration is not revealed (R8).
/// The state lives in memory: it resets on restart and is per instance.
/// </summary>
/// <remarks>
/// Same rules as <c>CheckPasswordSignInAsync(lockoutOnFailure: true)</c>: attempts during a lockout are
/// refused and not counted, the counter has no decay, and it resets when the lockout triggers.
/// </remarks>
public sealed class FailedLoginThrottle(IMemoryCache cache, TimeProvider timeProvider, IOptions<LoginLockoutOptions> lockoutOptions)
{
    // An entry with a counter and no lockout is dropped after this much inactivity, to bound memory. The
    // counter itself does not decay within a day; the lockout rules are the ones Identity applies.
    private static readonly TimeSpan s_idleEviction = TimeSpan.FromDays(1);

    /// <summary>Whether attempts for <paramref name="email"/> are currently refused.</summary>
    public bool IsLockedOut(string email, out DateTimeOffset retryAfter)
    {
        if (cache.TryGetValue(KeyFor(email), out Attempts? attempts) && attempts is not null)
        {
            lock (attempts)
            {
                if (attempts.LockoutEnd is { } end && end > timeProvider.GetUtcNow())
                {
                    retryAfter = end;

                    return true;
                }
            }
        }

        retryAfter = default;

        return false;
    }

    /// <summary>Counts a failed attempt. Call it only when <see cref="IsLockedOut"/> was <c>false</c>.</summary>
    /// <returns>The lockout when this failure triggered it, otherwise <c>null</c>.</returns>
    public PasswordCheckResult.LockoutTriggered? RegisterFailure(string email)
    {
        string key = KeyFor(email);
        DateTimeOffset now = timeProvider.GetUtcNow();
        LoginLockoutOptions lockout = lockoutOptions.Value;

        Attempts attempts = cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = s_idleEviction;

            return new Attempts();
        })!;

        lock (attempts)
        {
            if (attempts.LockoutEnd is { } end && end > now)
            {
                return null;
            }

            attempts.LockoutEnd = null;
            attempts.Failures++;

            if (attempts.Failures < lockout.MaxFailedAccessAttempts)
            {
                return null;
            }

            attempts.Failures = 0;
            attempts.LockoutEnd = now + lockout.Duration;

            return new PasswordCheckResult.LockoutTriggered(attempts.LockoutEnd.Value);
        }
    }

    // The key is a hash so the cache never holds an address.
    private static string KeyFor(string email) =>
        "failed-login:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())));

    private sealed class Attempts
    {
        public int Failures { get; set; }

        public DateTimeOffset? LockoutEnd { get; set; }
    }
}
