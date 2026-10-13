using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.GetExternalProviders;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class GetExternalProvidersEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.ExternalProviders, HandleAsync)
            .AllowAnonymous()
            .Produces<GetExternalProvidersResponse>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(IQueryHandler<GetExternalProvidersQuery, GetExternalProvidersResponse> handler, CancellationToken ct)
    {
        Result<GetExternalProvidersResponse> result = await handler.HandleAsync(new GetExternalProvidersQuery(), ct);

        return Results.Ok(result.Value);
    }
}
