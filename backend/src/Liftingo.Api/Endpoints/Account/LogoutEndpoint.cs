using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.Logout;

namespace Liftingo.Api.Endpoints.Account;

// No request body and no response body: the refresh token travels in the HttpOnly cookie, so a user whose
// access token has expired can still sign out.
internal sealed class LogoutEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.Logout, HandleAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        ICommandHandler<LogoutCommand> handler,
        RefreshTokenCookie refreshCookie,
        CancellationToken ct)
    {
        await handler.HandleAsync(new LogoutCommand(RefreshTokenCookie.Read(http.Request), ClientInfoReader.Read(http)), ct);

        refreshCookie.Delete(http.Response);

        return Results.NoContent();
    }
}
