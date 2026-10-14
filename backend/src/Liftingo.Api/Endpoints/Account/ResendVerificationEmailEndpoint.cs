using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.ResendVerificationEmail;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class ResendVerificationEmailEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ResendVerificationEmail, HandleAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(ICommandHandler<ResendVerificationEmailCommand> handler, CancellationToken ct)
    {
        await handler.HandleAsync(new ResendVerificationEmailCommand(), ct);

        return Results.Accepted();
    }
}