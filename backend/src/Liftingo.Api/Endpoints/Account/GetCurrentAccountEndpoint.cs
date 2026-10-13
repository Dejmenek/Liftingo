using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.GetCurrentAccount;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class GetCurrentAccountEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.Me, HandleAsync)
            .RequireAuthorization()
            .Produces<GetCurrentAccountResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(IQueryHandler<GetCurrentAccountQuery, GetCurrentAccountResponse> handler, CancellationToken ct)
    {
        Result<GetCurrentAccountResponse> result = await handler.HandleAsync(new GetCurrentAccountQuery(), ct);

        // The only failure is an account deleted after its token was issued: the same bare 401 the
        // authentication middleware sends for any other unusable token.
        return result.IsSuccess ? Results.Ok(result.Value) : Results.Unauthorized();
    }
}
