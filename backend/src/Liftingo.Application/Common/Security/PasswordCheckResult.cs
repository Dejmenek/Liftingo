namespace Liftingo.Application.Common.Security;

/// <summary>The outcome of <see cref="IIdentityService.CheckPasswordAsync"/>, with Identity's lockout rules applied.</summary>
public abstract record PasswordCheckResult
{
    // Closed hierarchy: only the nested cases below derive from it.
    private PasswordCheckResult()
    {
    }

    public sealed record Succeeded : PasswordCheckResult;

    public sealed record Failed : PasswordCheckResult;

    /// <summary>The account was already locked. The password was not checked and nothing changed.</summary>
    public sealed record LockedOut(DateTimeOffset RetryAfter) : PasswordCheckResult;

    /// <summary>This wrong password was the one that locked the account.</summary>
    public sealed record LockoutTriggered(DateTimeOffset RetryAfter) : PasswordCheckResult;
}
