namespace Liftingo.Application.Common.Security;

/// <summary>
/// The failed-login counter and lockout for e-mail addresses that have no account. It applies Identity's lockout
/// rules, so a lockout answer never reveals whether an address is registered. The state lives in the
/// shared session store, so it is the same on every instance and survives a restart.
/// </summary>
/// <remarks>
/// Attempts during a lockout are refused and not counted, the counter has no decay, and it resets when the
/// lockout triggers. Store outages surface as <c>SessionStoreUnavailableException</c>.
/// </remarks>
public interface ILoginThrottle
{
    /// <returns>When the lockout of <paramref name="email"/> ends, or <c>null</c> when it is not locked.</returns>
    Task<DateTimeOffset?> GetLockoutEndAsync(string email, CancellationToken ct);

    /// <summary>Counts a failed attempt. An attempt during a lockout is not counted.</summary>
    /// <returns>The lockout when this failure triggered it, otherwise <c>null</c>.</returns>
    Task<PasswordCheckResult.LockoutTriggered?> RegisterFailureAsync(string email, CancellationToken ct);
}
