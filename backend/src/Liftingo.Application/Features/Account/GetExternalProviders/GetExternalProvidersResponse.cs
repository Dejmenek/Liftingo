namespace Liftingo.Application.Features.Account.GetExternalProviders;

/// <param name="Providers">Route names (<c>google</c>, <c>facebook</c>, <c>x</c>) of the configured providers.</param>
public sealed record GetExternalProvidersResponse(IReadOnlyList<string> Providers);
