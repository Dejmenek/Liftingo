using Liftingo.Application.Common.Security;

namespace Liftingo.Infrastructure.Identity;

/// <summary>An external provider that was registered: its route name and its authentication scheme name.</summary>
internal sealed record ExternalProvider(string RouteName, string Scheme);

internal sealed class ExternalProviderCatalog : IExternalProviderCatalog
{
    private readonly IReadOnlyList<ExternalProvider> _providers;

    public ExternalProviderCatalog(IReadOnlyList<ExternalProvider> providers)
    {
        _providers = providers;
        RouteNames = [.. providers.Select(provider => provider.RouteName)];
    }

    public IReadOnlyList<string> RouteNames { get; }

    public string? FindScheme(string routeName) =>
        _providers.FirstOrDefault(p => string.Equals(p.RouteName, routeName, StringComparison.OrdinalIgnoreCase))?.Scheme;

    public string? FindRouteName(string scheme) =>
        _providers.FirstOrDefault(p => string.Equals(p.Scheme, scheme, StringComparison.Ordinal))?.RouteName;
}
