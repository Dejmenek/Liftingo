using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.Login;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class LoginEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.Login, HandleAsync)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        HttpContext http,
        ICommandHandler<LoginCommand, LoginResponse> handler,
        RefreshTokenCookie refreshCookie,
        CancellationToken ct)
    {
        Result<LoginResponse> result = await handler.HandleAsync(
            new LoginCommand(request.Email, request.Password, ClientInfoReader.Read(http)),
            ct);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        refreshCookie.Append(http.Response, result.Value.Session);

        return Results.Ok(AuthSessionResponse.From(result.Value.Session));
    }
}