using Liftingo.Api.Endpoints;

namespace Liftingo.Api.Security;

/// <summary>Where the provider handlers send the browser back to this API.</summary>
internal static class ExternalLoginPaths
{
    // The redirect target has to be a full path, version prefix included.
    public const string Callback = V1Endpoints.PathPrefix + RouteConsts.Auth.ExternalLoginCallback;
}
