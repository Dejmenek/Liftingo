using Liftingo.Api.Endpoints.Account.Shared;

namespace Liftingo.Api.Endpoints.Account;

/// <summary>
/// The user declined at the consent step (US5-2): drop the external cookie and with it everything the provider told
/// us. There is no use case behind it: no data, no rule, no audit (R19).
/// </summary>
internal sealed class CancelExternalLoginEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.CancelExternalLogin, (Delegate)HandleAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

    // It needs nothing but the HttpContext, so it is mapped as a plain delegate, not a RequestDelegate.
    private static async Task<IResult> HandleAsync(HttpContext http)
    {
        await ExternalLoginCookie.ClearAsync(http);

        return Results.NoContent();
    }
}
