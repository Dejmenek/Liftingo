namespace Liftingo.Application.Common.Security;

/// <summary>
/// The external sign-in providers that are configured, and how their route names (<c>google</c>, <c>facebook</c>,
/// <c>x</c>) map to authentication scheme names (<c>Google</c>, <c>Facebook</c>, <c>X</c>). The scheme name is also
/// what Identity stores as the login provider.
/// </summary>
public interface IExternalProviderCatalog
{
    /// <summary>Route names of the configured providers, in a stable order.</summary>
    IReadOnlyList<string> RouteNames { get; }

    /// <returns>The scheme name, or <c>null</c> when the provider is unknown or not configured.</returns>
    string? FindScheme(string routeName);

    /// <returns>The route name of a scheme, or <c>null</c> when the scheme is not a configured provider.</returns>
    string? FindRouteName(string scheme);
}
