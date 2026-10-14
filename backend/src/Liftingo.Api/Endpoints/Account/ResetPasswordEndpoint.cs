using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.ResetPassword;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class ResetPasswordEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ResetPassword, HandleAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        ResetPasswordRequest request,
        HttpContext http,
        ICommandHandler<ResetPasswordCommand> handler,
        RefreshTokenCookie refreshCookie,
        CancellationToken ct)
    {
        var command = new ResetPasswordCommand(
            request.Token,
            request.NewPassword,
            request.AcceptTermsAndAge,
            request.AcceptHealthData,
            request.AcceptMarketing,
            ClientInfoReader.Read(http));

        Result result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        refreshCookie.Delete(http.Response);

        return Results.NoContent();
    }
}