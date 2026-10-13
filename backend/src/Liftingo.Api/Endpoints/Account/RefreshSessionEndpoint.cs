using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.RefreshSession;

namespace Liftingo.Api.Endpoints.Account;

// No request body: the refresh token travels in the HttpOnly cookie.
internal sealed class RefreshSessionEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.RefreshSession, HandleAsync)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        ICommandHandler<RefreshSessionCommand, RefreshSessionResponse> handler,
        RefreshTokenCookie refreshCookie,
        CancellationToken ct)
    {
        Result<RefreshSessionResponse> result = await handler.HandleAsync(
            new RefreshSessionCommand(RefreshTokenCookie.Read(http.Request), ClientInfoReader.Read(http)),
            ct);

        if (result.IsFailure)
        {
            // Whatever went wrong, the cookie is of no further use.
            refreshCookie.Delete(http.Response);

            return result.ToProblem();
        }

        refreshCookie.Append(http.Response, result.Value.Session);

        return Results.Ok(AuthSessionResponse.From(result.Value.Session));
    }
}
