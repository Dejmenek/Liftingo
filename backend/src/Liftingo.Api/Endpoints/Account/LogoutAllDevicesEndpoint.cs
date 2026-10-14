using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.LogoutAllDevices;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class LogoutAllDevicesEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.LogoutAllDevices, HandleAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        ICommandHandler<LogoutAllDevicesCommand> handler,
        RefreshTokenCookie refreshCookie,
        CancellationToken ct)
    {
        await handler.HandleAsync(new LogoutAllDevicesCommand(ClientInfoReader.Read(http)), ct);

        refreshCookie.Delete(http.Response);

        return Results.NoContent();
    }
}
