namespace Liftingo.Application.Common.Security;

/// <summary>
/// The operations ASP.NET Core Identity owns. Every method only stages changes on the scoped
/// DbContext and never saves: the handler calls <c>SaveChangesAsync</c> once.
/// </summary>
public interface IIdentityService
{
    /// <summary>Password-policy failures come back as a <see cref="Result.ValidationError"/>.</summary>
    Task<Result.Result<Guid>> CreateUserAsync(
        string email,
        string? password,
        bool emailConfirmed,
        DateTimeOffset now,
        CancellationToken ct);

    Task<AccountSummary?> FindByEmailAsync(string email, CancellationToken ct);

    Task<AccountSummary?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken ct);

    Task<AccountSummary?> GetSummaryAsync(Guid userId, CancellationToken ct);

    Task<PasswordCheckResult> CheckPasswordAsync(Guid userId, string password, CancellationToken ct);

    /// <returns>The end of the current lockout, or <c>null</c> when the account is not locked.</returns>
    Task<DateTimeOffset?> GetLockoutEndAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Verifies the password against a fixed dummy hash, so a login for an unknown e-mail takes as
    /// long as one for a real account.
    /// </summary>
    Task VerifyDummyPasswordAsync(string password);

    Task AddLoginAsync(Guid userId, string loginProvider, string providerKey, string? displayName, CancellationToken ct);

    Task ConfirmEmailAsync(Guid userId, CancellationToken ct);

    Task RemovePasswordAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Sets the password (or adds one to a provider-only account), clears the failed-attempt counter and
    /// the lockout, and updates the security stamp.
    /// </summary>
    Task<Result.Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct);
}
