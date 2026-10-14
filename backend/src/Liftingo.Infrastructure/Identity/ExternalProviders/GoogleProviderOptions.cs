namespace Liftingo.Infrastructure.Identity.ExternalProviders;

/// <summary>
/// <c>Authentication:Google</c>. Checking an ID token needs only the client id, never the secret.
/// </summary>
internal sealed class GoogleProviderOptions
{
    public const string SectionName = "Authentication:Google";

    // Stored by Identity as the login provider, so it must not change once accounts are linked.
    public const string LoginProvider = "Google";

    public const string RouteName = "google";

    public string ClientId { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}