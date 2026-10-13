using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.Register;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class RegisterEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.Register, HandleAsync)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> HandleAsync(
        RegisterRequest request,
        HttpContext http,
        ICommandHandler<RegisterCommand, RegisterResponse> handler,
        RefreshTokenCookie refreshCookie,
        CancellationToken ct)
    {
        var command = new RegisterCommand(
            request.Email,
            request.Password,
            request.AcceptTermsAndAge,
            request.AcceptHealthData,
            request.AcceptMarketing,
            ClientInfoReader.Read(http));

        Result<RegisterResponse> result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        refreshCookie.Append(http.Response, result.Value.Session);

        return Results.Json(AuthSessionResponse.From(result.Value.Session), statusCode: StatusCodes.Status201Created);
    }
}
