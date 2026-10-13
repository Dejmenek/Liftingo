using System.Security.Claims;

using AspNet.Security.OAuth.Twitter;

using Liftingo.Application.Common.Security;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Infrastructure.Identity;

/// <summary>
/// Registers the external sign-in providers. A provider is registered only when both of its secrets are
/// configured, so a missing section switches the provider off instead of failing at startup. Api owns the scheme
/// that holds the provider's result and the callback path, and passes them in.
/// </summary>
public static class ExternalProviderOptions
{
    public const string GoogleScheme = "Google";
    public const string FacebookScheme = "Facebook";
    public const string XScheme = "X";

    public const string XCallbackPath = "/signin-x";

    private const string SectionName = "Authentication";

    /// <param name="builder">The authentication builder Api is setting up.</param>
    /// <param name="configuration">Holds the <c>Authentication:{Provider}</c> secrets.</param>
    /// <param name="signInScheme">The scheme that keeps the provider's result until the user gives consent.</param>
    /// <param name="callbackPath">Where a failed provider sign-in sends the browser, so it ends up on the login page.</param>
    public static AuthenticationBuilder AddExternalProviders(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        string signInScheme,
        string callbackPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(signInScheme);
        ArgumentException.ThrowIfNullOrEmpty(callbackPath);

        List<ExternalProvider> registered = [];

        if (ReadPair(configuration, GoogleScheme, "ClientId", "ClientSecret") is var (googleId, googleSecret))
        {
            builder.AddGoogle(GoogleScheme, options =>
            {
                options.ClientId = googleId;
                options.ClientSecret = googleSecret;
                ConfigureCommon(options, signInScheme, callbackPath);
            });
            registered.Add(new ExternalProvider("google", GoogleScheme));
        }

        if (ReadPair(configuration, FacebookScheme, "AppId", "AppSecret") is var (facebookId, facebookSecret))
        {
            builder.AddFacebook(FacebookScheme, options =>
            {
                options.AppId = facebookId;
                options.AppSecret = facebookSecret;
                ConfigureCommon(options, signInScheme, callbackPath);
            });
            registered.Add(new ExternalProvider("facebook", FacebookScheme));
        }

        if (ReadPair(configuration, XScheme, "ClientId", "ClientSecret") is var (xId, xSecret))
        {
            builder.AddTwitter(XScheme, XScheme, options =>
            {
                options.ClientId = xId;
                options.ClientSecret = xSecret;
                ConfigureX(options, signInScheme, callbackPath);
            });
            registered.Add(new ExternalProvider("x", XScheme));
        }

        builder.Services.AddSingleton<IExternalProviderCatalog>(new ExternalProviderCatalog(registered));

        return builder;
    }

    // Registered on top of the package's own defaults, which read neither the e-mail nor a verified flag from X:
    // the default scopes are tweet.read and users.read, UserFields is empty, and only id, username and name are mapped.
    internal static void ConfigureX(TwitterAuthenticationOptions options, string signInScheme, string callbackPath)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.CallbackPath = XCallbackPath;
        options.Scope.Add("users.email");
        options.UserFields.Add("confirmed_email");
        options.ClaimActions.MapJsonSubKey(ClaimTypes.Email, "data", "confirmed_email");

        ConfigureCommon(options, signInScheme, callbackPath);
    }

    private static void ConfigureCommon(OAuthOptions options, string signInScheme, string callbackPath)
    {
        options.SignInScheme = signInScheme;

        // A provider error, a cancelled consent screen or a bad state would make the handler throw. Send the browser
        // to the callback endpoint instead, which finds no external principal and redirects to the login page.
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect(callbackPath);
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
