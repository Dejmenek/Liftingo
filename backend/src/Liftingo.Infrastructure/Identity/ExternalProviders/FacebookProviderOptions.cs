namespace Liftingo.Infrastructure.Identity.ExternalProviders;

/// <summary><c>Authentication:Facebook</c>.</summary>
internal sealed class FacebookProviderOptions
{
    public const string SectionName = "Authentication:Facebook";

    // Stored by Identity as the login provider, so it must not change once accounts are linked.
    public const string LoginProvider = "Facebook";

    public const string RouteName = "facebook";

    public const string DefaultGraphApiVersion = "v26.0";

    public string AppId { get; init; } = string.Empty;

    public string AppSecret { get; init; } = string.Empty;

    public string GraphApiVersion { get; init; } = DefaultGraphApiVersion;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppSecret);
}