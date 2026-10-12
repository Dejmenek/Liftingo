using Liftingo.Application.Common;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

// No request body. The response is the shared AccountResponse (Shared/AuthSessionResponse.cs).

public sealed class GetCurrentAccount : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.Me, Handle)
            .RequireAuthorization()
            .Produces<AccountResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> Handle(
        IUserContext userContext,
        IIdentityService identity,
        CancellationToken ct)
    {
        AccountSummary? summary = await identity.GetSummaryAsync(userContext.UserId, ct);

        // A valid token for an account that no longer exists (deleted after the token was issued).
        return summary is null
            ? Results.Unauthorized()
            : Results.Ok(AccountResponse.From(summary));
    }
}
