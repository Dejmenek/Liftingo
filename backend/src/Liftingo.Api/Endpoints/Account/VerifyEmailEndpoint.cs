using Liftingo.Api.Http;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.VerifyEmail;

namespace Liftingo.Api.Endpoints.Account;

// Anonymous on purpose: the token identifies the account, so a signed-in different user is ignored.
internal sealed class VerifyEmailEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.VerifyEmail, HandleAsync)
            .AllowAnonymous()
            .Produces<VerifyEmailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<IResult> HandleAsync(VerifyEmailCommand command, ICommandHandler<VerifyEmailCommand, VerifyEmailResponse> handler, CancellationToken ct)
    {
        Result<VerifyEmailResponse> result = await handler.HandleAsync(command, ct);

        return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblem();
    }
}
