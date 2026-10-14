namespace Liftingo.Api.Endpoints;

internal interface IEndpoint
{
    static abstract void Map(RouteGroupBuilder group);
}
