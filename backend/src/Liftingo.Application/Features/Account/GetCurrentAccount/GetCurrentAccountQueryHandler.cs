using Liftingo.Application.Common;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.GetCurrentAccount;

public sealed class GetCurrentAccountQueryHandler(IUserContext userContext, IIdentityService identity) : IQueryHandler<GetCurrentAccountQuery, GetCurrentAccountResponse>
{
    // A valid token for an account that no longer exists (deleted after the token was issued).
    private static readonly Error s_accountNotFound =
        Error.Unauthorized("Auth.AccountNotFound", "Konto nie istnieje.");

    public async Task<Result<GetCurrentAccountResponse>> HandleAsync(GetCurrentAccountQuery query, CancellationToken ct)
    {
        AccountSummary? summary = await identity.GetSummaryAsync(userContext.UserId, ct);

        return summary is null
            ? Result.Failure<GetCurrentAccountResponse>(s_accountNotFound)
            : GetCurrentAccountResponse.From(summary);
    }
}