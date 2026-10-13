namespace Liftingo.Api.Endpoints;

/// <summary>
/// Implemented by every endpoint to register its route. <see cref="V1Endpoints"/> lists the endpoints of the version
/// and calls <see cref="Map"/> with that version's route group, so endpoints never deal with the version prefix.
/// </summary>
internal interface IEndpoint
{
    static abstract void Map(RouteGroupBuilder group);
}
