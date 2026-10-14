using Liftingo.Api.Endpoints;
using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Security;
using Liftingo.Application.Common;

using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Liftingo.Api.Extensions;

internal static class AuthenticationExtensions
{
    /// <summary>
    /// Decides how requests are authenticated: the JWT bearer scheme, configured by Infrastructure through its JWT
    /// bearer options setup. Provider sign-in needs no scheme: the client posts the provider's token and the
    /// sign-in endpoint verifies it.
    /// </summary>
    public static IServiceCollection AddAuthenticationSettings(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddAuthorization();

        services.AddOptions<RefreshCookieOptions>()
            .Configure(options => options.Path = V1Endpoints.PathPrefix + RouteConsts.Auth.SessionPrefix);

        services.AddSingleton<RefreshTokenCookie>();

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();

        return services;
    }
}