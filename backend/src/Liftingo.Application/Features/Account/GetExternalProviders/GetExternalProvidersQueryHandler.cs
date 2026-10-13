using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.GetExternalProviders;

public sealed class GetExternalProvidersQueryHandler(IExternalProviderCatalog catalog)
{
    public Task<Result<GetExternalProvidersResponse>> HandleAsync(GetExternalProvidersQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        Result<GetExternalProvidersResponse> result = new GetExternalProvidersResponse(catalog.RouteNames);

        return Task.FromResult(result);
    }
}
