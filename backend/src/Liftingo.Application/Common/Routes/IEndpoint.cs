using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Common.Routes;

/// <summary>
/// Implemented by every vertical slice to register its route. <c>Api</c> lists the slices of each API version
/// and calls <see cref="Map"/> with that version's route group, so slices never deal with the version prefix.
/// </summary>
public interface IEndpoint
{
    static abstract void Map(RouteGroupBuilder group);
}