using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.GetPendingExternalLogin;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class GetPendingExternalLoginEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.PendingExternalLogin, HandleAsync)
            .AllowAnonymous()
            .Produces<GetPendingExternalLoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<IResult> HandleAsync(HttpContext http, GetPendingExternalLoginQueryHandler handler, CancellationToken ct)
    {
        Result<GetPendingExternalLoginResponse> result = await handler.HandleAsync(
            new GetPendingExternalLoginQuery(await ExternalLoginCookie.ReadAsync(http)),
            ct);

        return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblem();
    }
}
