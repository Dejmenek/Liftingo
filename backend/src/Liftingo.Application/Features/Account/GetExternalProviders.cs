using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

// No request body.

/// <param name="Providers">Route names (<c>google</c>, <c>facebook</c>, <c>x</c>) of the configured providers.</param>
public sealed record ExternalProvidersResponse(IReadOnlyList<string> Providers);

public sealed class GetExternalProviders : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.ExternalProviders, Handle)
            .AllowAnonymous()
            .Produces<ExternalProvidersResponse>(StatusCodes.Status200OK);

    private static IResult Handle(IExternalProviderCatalog catalog) =>
        Results.Ok(new ExternalProvidersResponse(catalog.RouteNames));
}
