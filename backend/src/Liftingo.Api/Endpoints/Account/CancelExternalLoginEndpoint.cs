using Liftingo.Api.Endpoints.Account.Shared;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class CancelExternalLoginEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.CancelExternalLogin, (Delegate)HandleAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

    private static async Task<IResult> HandleAsync(HttpContext http)
    {
        await ExternalLoginCookie.ClearAsync(http);

        return Results.NoContent();
    }
}
