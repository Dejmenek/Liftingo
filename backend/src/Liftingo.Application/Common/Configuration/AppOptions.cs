namespace Liftingo.Application.Common.Configuration;

/// <summary>
/// Settings of the app as a whole. Infrastructure builds e-mail links from them and Api builds redirects, so
/// they live here, where both outer layers can read them.
/// </summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    public Uri? FrontendBaseUrl { get; init; }
}
