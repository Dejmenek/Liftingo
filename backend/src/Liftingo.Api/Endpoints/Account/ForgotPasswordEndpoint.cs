using Liftingo.Api.Http;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.ForgotPassword;

namespace Liftingo.Api.Endpoints.Account;

// No response body: registered, unregistered and rate-limited addresses all get the same empty 202 (SC-008).
internal sealed class ForgotPasswordEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ForgotPassword, HandleAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<IResult> HandleAsync(ForgotPasswordCommand command, ICommandHandler<ForgotPasswordCommand> handler, CancellationToken ct)
    {
        Result result = await handler.HandleAsync(command, ct);

        return result.IsSuccess ? Results.Accepted() : result.ToProblem();
    }
}
