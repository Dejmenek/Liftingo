using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.HandleExternalLoginCallback;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Api.Endpoints.Account;

// No request body and no response body: always a 302 to the frontend, whatever happened.
internal sealed class HandleExternalLoginCallbackEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.ExternalLoginCallback, HandleAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status302Found);

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        ICommandHandler<HandleExternalLoginCallbackCommand, HandleExternalLoginCallbackResponse> handler,
        RefreshTokenCookie refreshCookie,
        FrontendRedirects redirects,
        CancellationToken ct)
    {
        ExternalIdentity? external = await ExternalLoginCookie.ReadAsync(http);

        // The device id sent to the start endpoint identifies the device better than this redirect's headers.
        Result<HandleExternalLoginCallbackResponse> result = await handler.HandleAsync(
            new HandleExternalLoginCallbackCommand(external, ClientInfoReader.Read(http, external?.DeviceId)),
            ct);

        // The consent step still needs the external cookie; every other outcome ends the provider flow.
        if (result is { IsSuccess: true, Value.Outcome: HandleExternalLoginCallbackOutcome.ConsentRequired })
        {
            return redirects.Consent();
        }

        await ExternalLoginCookie.ClearAsync(http);

        if (result is { IsSuccess: true, Value.Session: { } session })
        {
            refreshCookie.Append(http.Response, session);

            return redirects.Callback();
        }

        return result.Error switch
        {
            LockedOutError lockedOut => redirects.LockedOut(lockedOut.RetryAfter),
            { } error when error == ExternalLoginErrors.ProviderEmailConflict => redirects.EmailConflict(),
            _ => redirects.External(),
        };
    }
}
