using Liftingo.Application.Features.Account.ResendVerificationEmail;

namespace Liftingo.Api.Endpoints.Account;

// No request body and no response body. The signed-in user comes from the bearer token.
internal sealed class ResendVerificationEmailEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ResendVerificationEmail, HandleAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    // 202 whatever happens, so the answer says nothing about the limit or the account state (FR-037).
    private static async Task<IResult> HandleAsync(ResendVerificationEmailCommandHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(new ResendVerificationEmailCommand(), ct);

        return Results.Accepted();
    }
}
