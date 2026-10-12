using Liftingo.Api.Endpoints;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Api.Extensions;

internal static class AuthenticationExtensions
{
    public static IServiceCollection AddAuthenticationSettings(this IServiceCollection services)
    {
        // The refresh cookie is sent only to the session endpoints. The path needs the API version prefix,
        // which Application does not know.
        services.AddOptions<SessionsOptions>()
            .PostConfigure(options => options.CookiePath = V1Endpoints.PathPrefix + RouteConsts.Auth.SessionPrefix);

        return services;
    }
}
