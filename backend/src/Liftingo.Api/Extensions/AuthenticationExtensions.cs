using Liftingo.Api.Endpoints;
using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Security;
using Liftingo.Application.Common;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Infrastructure.Identity;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Liftingo.Api.Extensions;

internal static class AuthenticationExtensions
{
    /// <summary>
    /// Decides how requests are authenticated: the JWT bearer scheme by default, the short-lived external cookie
    /// for a pending provider sign-in, and the provider handlers. Infrastructure supplies the provider and token
    /// configuration through <c>AddExternalProviders</c> and its JWT bearer options setup.
    /// </summary>
    public static IServiceCollection AddAuthenticationSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddCookie(AuthenticationSchemes.External, options =>
            {
                options.Cookie.Name = AuthenticationSchemes.External;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
            })
            .AddExternalProviders(configuration, AuthenticationSchemes.External, ExternalLoginPaths.Callback);

        services.AddOptions<CookieAuthenticationOptions>(AuthenticationSchemes.External)
            .Configure<IOptions<ExternalLoginOptions>, TimeProvider>((cookie, externalLogin, timeProvider) =>
            {
                cookie.ExpireTimeSpan = externalLogin.Value.PendingTimeout;

                // The 10-minute pending window follows the injected clock, like token lifetimes do.
                cookie.TimeProvider = timeProvider;
            });

        services.AddAuthorization();

        // The refresh cookie is sent only to the session endpoints, so its path needs the API version prefix.
        services.AddOptions<RefreshCookieOptions>()
            .Configure(options => options.Path = V1Endpoints.PathPrefix + RouteConsts.Auth.SessionPrefix);

        services.AddSingleton<RefreshTokenCookie>();
        services.AddSingleton<FrontendRedirects>();

        // The signed-in user comes from the access token on the current request.
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();

        return services;
    }
}
