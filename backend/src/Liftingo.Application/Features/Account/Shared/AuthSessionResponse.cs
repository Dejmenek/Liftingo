using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.Shared;

// The body of every endpoint that signs a user in. Five slices return it, so it lives here instead of in a slice file.
public sealed record AuthSessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AccountResponse Account);

public sealed record AccountResponse(
    Guid Id,
    string Email,
    bool EmailVerified,
    bool HasPassword,
    IReadOnlyList<string> Providers)
{
    public static AccountResponse From(AccountSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new AccountResponse(summary.Id, summary.Email, summary.EmailVerified, summary.HasPassword, summary.Providers);
    }
}
