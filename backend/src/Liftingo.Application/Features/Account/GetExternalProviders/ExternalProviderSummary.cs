namespace Liftingo.Application.Features.Account.GetExternalProviders;

/// <param name="Name">The route name, e.g. <c>google</c>.</param>
/// <param name="ClientId">The public id the provider's sign-in script needs (Google client id, Facebook app id).</param>
public sealed record ExternalProviderSummary(string Name, string ClientId);
