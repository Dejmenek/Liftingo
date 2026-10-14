using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.CompleteExternalLogin;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class CompleteExternalLoginEndpoint : IEndpoint
{
    private static readonly ExternalLoginLinkConfirmationResponse s_linkConfirmationSent =
        new(ExternalLoginLinkConfirmationResponse.LinkConfirmationSent);

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.CompleteExternalLogin, HandleAsync)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<ExternalLoginLinkConfirmationResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        CompleteExternalLoginRequest request,
        HttpContext http,
        ICommandHandler<CompleteExternalLoginCommand, CompleteExternalLoginResponse> handler,
        RefreshTokenCookie refreshCookie,
        CancellationToken ct)
    {
        ExternalIdentity? external = await ExternalLoginCookie.ReadAsync(http);

        var command = new CompleteExternalLoginCommand(
            request.AcceptTermsAndAge,
            request.AcceptHealthData,
            request.AcceptMarketing,
            request.Email,
            external,
            ClientInfoReader.ReadPreferringHeader(http, external?.DeviceId));

        Result<CompleteExternalLoginResponse> result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        await ExternalLoginCookie.ClearAsync(http);

        if (result.Value.Session is not { } session)
        {
            return Results.Json(s_linkConfirmationSent, statusCode: StatusCodes.Status202Accepted);
        }

        refreshCookie.Append(http.Response, session);
        AuthSessionResponse body = AuthSessionResponse.From(session);

        return result.Value.Outcome == CompleteExternalLoginOutcome.AccountCreated
            ? Results.Json(body, statusCode: StatusCodes.Status201Created)
            : Results.Ok(body);
    }
}
