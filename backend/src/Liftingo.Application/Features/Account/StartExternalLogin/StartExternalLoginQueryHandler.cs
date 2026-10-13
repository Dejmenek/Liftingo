using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.StartExternalLogin;

/// <summary>Resolves the provider to its authentication scheme. Api sends the browser to the provider.</summary>
public sealed class StartExternalLoginQueryHandler(IExternalProviderCatalog catalog) : IQueryHandler<StartExternalLoginQuery, StartExternalLoginResponse>
{
    private static readonly Error s_providerUnavailable = Error.NotFound(
        "Auth.ProviderUnavailable",
        "Logowanie przez tego dostawcę jest niedostępne.");

    public Task<Result<StartExternalLoginResponse>> HandleAsync(StartExternalLoginQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        Result<StartExternalLoginResponse> result = catalog.FindScheme(query.Provider) is { } scheme
            ? new StartExternalLoginResponse(scheme)
            : Result.Failure<StartExternalLoginResponse>(s_providerUnavailable);

        return Task.FromResult(result);
    }
}
