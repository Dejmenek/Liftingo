namespace Liftingo.Application.Common.Security;

/// <param name="RouteName">The name in routes, e.g. <c>google</c>.</param>
/// <param name="LoginProvider">The name Identity stores with a linked login, e.g. <c>Google</c>.</param>
/// <param name="ClientId">The public id the provider's sign-in script needs (Google client id, Facebook app id).</param>
public sealed record ExternalProviderInfo(string RouteName, string LoginProvider, string ClientId);
