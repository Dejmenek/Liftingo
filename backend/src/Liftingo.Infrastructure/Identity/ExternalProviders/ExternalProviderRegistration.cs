using Liftingo.Application.Common.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Infrastructure.Identity.ExternalProviders;

internal static class ExternalProviderRegistration
{
    private const string GraphApiHost = "https://graph.facebook.com/";

    /// <summary>
    /// Registers a verifier for each provider whose keys are configured. A missing section switches the provider
    /// off instead of failing at startup, and the provider is then left out of the catalog.
    /// </summary>
    public static IServiceCollection AddExternalProviders(this IServiceCollection services, IConfiguration configuration)
    {
        List<ExternalProviderInfo> providers = [];

        GoogleProviderOptions google =
            configuration.GetSection(GoogleProviderOptions.SectionName).Get<GoogleProviderOptions>() ?? new();

        if (google.IsConfigured)
        {
            services.AddSingleton(google);
            services.AddSingleton<IProviderTokenVerifier, GoogleIdTokenVerifier>();
            providers.Add(new ExternalProviderInfo(GoogleProviderOptions.RouteName, GoogleProviderOptions.LoginProvider, google.ClientId));
        }

        FacebookProviderOptions facebook =
            configuration.GetSection(FacebookProviderOptions.SectionName).Get<FacebookProviderOptions>() ?? new();

        if (facebook.IsConfigured)
        {
            services.AddSingleton(facebook);

            // The request URIs carry the user's token and the app secret, so the client's own request logging is
            // switched off.
            services
                .AddHttpClient<FacebookTokenVerifier>(client =>
                    client.BaseAddress = new Uri($"{GraphApiHost}{facebook.GraphApiVersion}/"))
                .RemoveAllLoggers();
            services.AddTransient<IProviderTokenVerifier>(provider => provider.GetRequiredService<FacebookTokenVerifier>());
            providers.Add(new ExternalProviderInfo(FacebookProviderOptions.RouteName, FacebookProviderOptions.LoginProvider, facebook.AppId));
        }

        services.AddSingleton<IExternalProviderCatalog>(new ExternalProviderCatalog(providers));
        services.AddScoped<IExternalIdentityVerifier, ExternalIdentityVerifier>();

        return services;
    }
}
