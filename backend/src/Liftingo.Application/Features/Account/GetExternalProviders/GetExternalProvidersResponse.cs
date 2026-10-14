namespace Liftingo.Application.Features.Account.GetExternalProviders;

/// <param name="Providers">The configured providers, in a stable order.</param>
public sealed record GetExternalProvidersResponse(IReadOnlyList<ExternalProviderSummary> Providers);