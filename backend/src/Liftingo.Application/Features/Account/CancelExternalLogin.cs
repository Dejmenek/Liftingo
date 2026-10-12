using Liftingo.Application.Common.Routes;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

// No request body and no response body.

public sealed class CancelExternalLogin : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.CancelExternalLogin, (Delegate)Handle)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

    // The user declined at the consent step (US5-2): drop the cookie and with it everything the provider told us.
    // The handler needs nothing but the HttpContext, so it is mapped as a plain delegate, not a RequestDelegate.
    private static async Task<IResult> Handle(HttpContext http)
    {
        await PendingExternalLogin.ClearAsync(http);

        return Results.NoContent();
    }
}
