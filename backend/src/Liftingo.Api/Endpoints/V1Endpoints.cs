using Asp.Versioning;

using Liftingo.Application.Features.Common;

namespace Liftingo.Api.Endpoints;

internal static class V1Endpoints
{
    public static readonly ApiVersion Version = new(1, 0);

    public static RouteGroupBuilder MapV1Endpoints(this RouteGroupBuilder v1)
    {
        Ping.Map(v1);

        return v1;
    }
}