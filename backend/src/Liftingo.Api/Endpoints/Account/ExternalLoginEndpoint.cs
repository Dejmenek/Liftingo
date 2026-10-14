using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.ExternalLogin;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class ExternalLoginEndpoint : IEndpoint
{
    private static readonly ExternalLoginLinkConfirmationResponse s_linkConfirmationSent =
        new(ExternalLoginLinkConfirmationResponse.LinkConfirmationSent);

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ExternalLogin, HandleAsync)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<ExternalLoginLinkConfirmationResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        string provider,
        ExternalLoginRequest request,
        HttpContext http,
        ICommandHandler<ExternalLoginCommand, ExternalLoginResponse> handler,
        RefreshTokenCookie refreshCookie,
        CancellationToken ct)
    {
        var command = new ExternalLoginCommand(
            provider,
            request.Token,
            request.AcceptTermsAndAge,
            request.AcceptHealthData,
            request.AcceptMarketing,
            request.Email,
            ClientInfoReader.Read(http));

        Result<ExternalLoginResponse> result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        if (result.Value.Session is not { } session)
        {
            return Results.Json(s_linkConfirmationSent, statusCode: StatusCodes.Status202Accepted);
        }

        refreshCookie.Append(http.Response, session);
        AuthSessionResponse body = AuthSessionResponse.From(session);

        return result.Value.Outcome == ExternalLoginOutcome.AccountCreated
            ? Results.Json(body, statusCode: StatusCodes.Status201Created)
            : Results.Ok(body);
    }
}
