using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

// No request body: the refresh token travels in the HttpOnly cookie. The response is the shared AuthSessionResponse.

public sealed class RefreshSession : IEndpoint
{
    private static readonly Error s_sessionInvalid =
        Error.Unauthorized("Auth.SessionInvalid", "Sesja wygasła lub jest nieprawidłowa. Zaloguj się ponownie.");

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.RefreshSession, Handle)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> Handle(
        HttpContext http,
        ISessionStore sessionStore,
        IIdentityService identity,
        IAccessTokenIssuer accessTokens,
        RefreshTokenCookie refreshCookie,
        SecurityAudit audit,
        CancellationToken ct)
    {
        byte[]? tokenHash = RefreshTokenCookie.Read(http.Request) is { } token ? OpaqueToken.Hash(token) : null;

        if (tokenHash is null)
        {
            return Invalid(http, refreshCookie);
        }

        switch (await sessionStore.RotateAsync(tokenHash, ct))
        {
            case RotationResult.Rotated rotated:
                // The e-mail may have been verified since the last token, so read the current state.
                AccountSummary? account = await identity.GetSummaryAsync(rotated.Session.UserId, ct);

                if (account is null)
                {
                    // The account was deleted while its session lived on.
                    await sessionStore.RevokeAsync(rotated.Session.SessionId, ct);

                    return Invalid(http, refreshCookie);
                }

                AccessToken accessToken = accessTokens.Issue(account.Id, rotated.Session.SessionId, account.EmailVerified);
                refreshCookie.Append(http.Response, rotated.NewRefreshToken, rotated.RemainingLifetime);

                return Results.Ok(new AuthSessionResponse(
                    accessToken.Value,
                    accessToken.ExpiresAt,
                    AccountResponse.From(account)));

            case RotationResult.Reused reused:
                // The session is already revoked by the store. Record it against the session's own device.
                await audit.WriteAsync(
                    reused.Session.UserId,
                    SecurityEventType.RefreshTokenReuse,
                    SecurityEventOutcome.Failed,
                    ClientInfo.From(http, reused.Session.Device.DeviceId) with { DeviceType = reused.Session.Device.DeviceType },
                    ct);

                return Invalid(http, refreshCookie);

            default:
                return Invalid(http, refreshCookie);
        }
    }

    private static IResult Invalid(HttpContext http, RefreshTokenCookie refreshCookie)
    {
        refreshCookie.Delete(http.Response);

        return Result.Failure(s_sessionInvalid).ToProblem();
    }
}
