using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.GetExternalProviders;

public sealed class GetExternalProvidersQueryHandler(IExternalProviderCatalog catalog) : IQueryHandler<GetExternalProvidersQuery, GetExternalProvidersResponse>
{
    public Task<Result<GetExternalProvidersResponse>> HandleAsync(GetExternalProvidersQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        Result<GetExternalProvidersResponse> result = new GetExternalProvidersResponse(
            [.. catalog.Providers.Select(provider => new ExternalProviderSummary(provider.RouteName, provider.ClientId))]);

        return Task.FromResult(result);
    }
}
