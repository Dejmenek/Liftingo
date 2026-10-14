using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.ConfirmProviderLink;

namespace Liftingo.Api.Endpoints.Account;

internal sealed class ConfirmProviderLinkEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ConfirmProviderLink, HandleAsync)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        ConfirmProviderLinkRequest request,
        HttpContext http,
        ICommandHandler<ConfirmProviderLinkCommand, ConfirmProviderLinkResponse> handler,
        RefreshTokenCookie refreshCookie,
        CancellationToken ct)
    {
        var command = new ConfirmProviderLinkCommand(
            request.Token,
            request.AcceptTermsAndAge,
            request.AcceptHealthData,
            request.AcceptMarketing,
            ClientInfoReader.Read(http));

        Result<ConfirmProviderLinkResponse> result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        refreshCookie.Append(http.Response, result.Value.Session);

        return Results.Ok(AuthSessionResponse.From(result.Value.Session));
    }
}