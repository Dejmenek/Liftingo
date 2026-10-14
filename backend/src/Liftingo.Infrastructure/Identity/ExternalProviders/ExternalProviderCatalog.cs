using Liftingo.Application.Common.Security;

namespace Liftingo.Infrastructure.Identity.ExternalProviders;

internal sealed class ExternalProviderCatalog(IReadOnlyList<ExternalProviderInfo> providers) : IExternalProviderCatalog
{
    public IReadOnlyList<ExternalProviderInfo> Providers { get; } = providers;

    public ExternalProviderInfo? Find(string routeName) =>
        Providers.FirstOrDefault(p => string.Equals(p.RouteName, routeName, StringComparison.OrdinalIgnoreCase));
}
