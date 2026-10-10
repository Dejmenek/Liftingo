using Liftingo.Application.Common.Routes;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Common;

// Placeholder that proves the versioned route registration end to end. Remove it once a real slice exists.
public sealed record PingResponse(string Status);

public sealed class Ping : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Ping, Handle);

    private static IResult Handle() => Results.Ok(new PingResponse("ok"));
}