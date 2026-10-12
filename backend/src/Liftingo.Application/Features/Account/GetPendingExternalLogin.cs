using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

// No request body.

/// <param name="Provider">Route name of the provider, e.g. <c>google</c>.</param>
/// <param name="Email">The provider's e-mail, or <c>null</c> when it gave none.</param>
/// <param name="EmailRequired"><c>true</c> when the user has to type an e-mail (FR-023).</param>
/// <param name="AccountExists"><c>true</c> when the provider e-mail belongs to an unverified account, which the sign-in would take over (R10).</param>
public sealed record PendingExternalLoginResponse(string Provider, string? Email, bool EmailRequired, bool AccountExists);

public sealed class GetPendingExternalLogin : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.PendingExternalLogin, Handle)
            .AllowAnonymous()
            .Produces<PendingExternalLoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<IResult> Handle(
        HttpContext http,
        IIdentityService identity,
        IExternalProviderCatalog catalog,
        CancellationToken ct)
    {
        PendingExternalLogin? pending = await PendingExternalLogin.ReadAsync(http);

        if (pending is null || catalog.FindRouteName(pending.Provider) is not { } provider)
        {
            return Result.Failure(ExternalLoginErrors.Expired).ToProblem();
        }

        bool accountExists = pending.Email is not null
            && await identity.FindByEmailAsync(pending.Email, ct) is { EmailVerified: false };

        return Results.Ok(new PendingExternalLoginResponse(provider, pending.Email, pending.Email is null, accountExists));
    }
}
