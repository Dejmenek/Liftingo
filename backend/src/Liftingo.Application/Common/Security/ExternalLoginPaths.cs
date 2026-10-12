using Liftingo.Application.Common.Routes;

namespace Liftingo.Application.Common.Security;

/// <summary>Where the provider handlers send the browser back to this API.</summary>
public static class ExternalLoginPaths
{
    // Application does not know the API version prefix elsewhere; the redirect target has to be a full path.
    public const string Callback = "/api/v1" + RouteConsts.Auth.ExternalLoginCallback;
}
