using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.GetCurrentAccount;

/// <param name="HasPassword">Whether the account can sign in with a password.</param>
public sealed record GetCurrentAccountResponse(
    Guid Id,
    string Email,
    bool EmailVerified,
    bool HasPassword,
    IReadOnlyList<string> Providers)
{
    public static GetCurrentAccountResponse From(AccountSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new GetCurrentAccountResponse(summary.Id, summary.Email, summary.EmailVerified, summary.HasPassword, summary.Providers);
    }
}