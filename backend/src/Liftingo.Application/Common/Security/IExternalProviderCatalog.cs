namespace Liftingo.Application.Common.Security;

/// <summary>The external sign-in providers that are configured.</summary>
public interface IExternalProviderCatalog
{
    /// <summary>The configured providers, in a stable order.</summary>
    IReadOnlyList<ExternalProviderInfo> Providers { get; }

    /// <returns>The provider, or <c>null</c> when it is unknown or not configured. Case is ignored.</returns>
    ExternalProviderInfo? Find(string routeName);
}