using Liftingo.Application.Common.Security;

namespace Liftingo.Api.Endpoints.Account.Shared;

public sealed record AccountResponse(
    Guid Id,
    string Email,
    bool EmailVerified,
    bool HasPassword,
    IReadOnlyList<string> Providers)
{
    public static AccountResponse From(AccountSummary summary)
    {
        return new AccountResponse(summary.Id, summary.Email, summary.EmailVerified, summary.HasPassword, summary.Providers);
    }
}
