using Asp.Versioning;

using Liftingo.Application.Features.Common;

namespace Liftingo.Api.Endpoints;

internal static class V1Endpoints
{
    // The concrete path prefix of this version, for things that need it outside routing (cookie paths).
    public const string PathPrefix = "/api/v1";

    public static readonly ApiVersion Version = new(1, 0);

    public static RouteGroupBuilder MapV1Endpoints(this RouteGroupBuilder v1)
    {
        Ping.Map(v1);

        return v1;
    }
}