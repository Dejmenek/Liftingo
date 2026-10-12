using System.Security.Claims;

using AspNet.Security.OAuth.Twitter;

using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Infrastructure.Identity;

/// <summary>
/// Registers the external sign-in providers (R9). A provider is registered only when both of its secrets are
/// configured, so a missing section switches the provider off instead of failing at startup.
/// </summary>
internal static class ExternalProviderOptions
{
    public const string GoogleScheme = "Google";
    public const string FacebookScheme = "Facebook";
    public const string XScheme = "X";

    public const string XCallbackPath = "/signin-x";

    private const string SectionName = "Authentication";

    public static AuthenticationBuilder AddExternalProviders(this AuthenticationBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        List<ExternalProvider> registered = [];

        if (ReadPair(configuration, GoogleScheme, "ClientId", "ClientSecret") is var (googleId, googleSecret))
        {
            builder.AddGoogle(GoogleScheme, options =>
            {
                options.ClientId = googleId;
                options.ClientSecret = googleSecret;
                ConfigureCommon(options);
            });
            registered.Add(new ExternalProvider("google", GoogleScheme));
        }

        if (ReadPair(configuration, FacebookScheme, "AppId", "AppSecret") is var (facebookId, facebookSecret))
        {
            builder.AddFacebook(FacebookScheme, options =>
            {
                options.AppId = facebookId;
                options.AppSecret = facebookSecret;
                ConfigureCommon(options);
            });
            registered.Add(new ExternalProvider("facebook", FacebookScheme));
        }

        if (ReadPair(configuration, XScheme, "ClientId", "ClientSecret") is var (xId, xSecret))
        {
            builder.AddTwitter(XScheme, XScheme, options =>
            {
                options.ClientId = xId;
                options.ClientSecret = xSecret;
                ConfigureX(options);
            });
            registered.Add(new ExternalProvider("x", XScheme));
        }

        builder.Services.AddSingleton<IExternalProviderCatalog>(new ExternalProviderCatalog(registered));

        return builder;
    }

    // Registered on top of the package's own defaults, which read neither the e-mail nor a verified flag from X:
    // the default scopes are tweet.read and users.read, UserFields is empty, and only id, username and name are mapped.
    internal static void ConfigureX(TwitterAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.CallbackPath = XCallbackPath;
        options.Scope.Add("users.email");
        options.UserFields.Add("confirmed_email");
        options.ClaimActions.MapJsonSubKey(ClaimTypes.Email, "data", "confirmed_email");

        ConfigureCommon(options);
    }

    private static void ConfigureCommon(OAuthOptions options)
    {
        options.SignInScheme = AuthenticationSchemes.External;

        // The scheme name travels in the external cookie with the rest of the properties, so Application never has to
        // guess the provider from a claims issuer (the X package, for one, issues its claims as "Twitter").
        options.Events.OnTicketReceived = context =>
        {
            context.Properties ??= new AuthenticationProperties();
            context.Properties.Items[PendingExternalLogin.ProviderItem] = context.Scheme.Name;

            return Task.CompletedTask;
        };

        // A provider error, a cancelled consent screen or a bad state would make the handler throw. Send the browser
        // to the callback endpoint instead, which finds no external principal and redirects to the login page (FR-020).
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect(ExternalLoginPaths.Callback);
            context.HandleResponse();

            return Task.CompletedTask;
        };
    }

    private static (string Id, string Secret)? ReadPair(IConfiguration configuration, string provider, string idKey, string secretKey)
    {
        IConfigurationSection section = configuration.GetSection($"{SectionName}:{provider}");
        string? id = section[idKey];
        string? secret = section[secretKey];

        return string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret) ? null : (id, secret);
    }
}
