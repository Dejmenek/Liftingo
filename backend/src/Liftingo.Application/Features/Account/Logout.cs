using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

// No request body and no response body: the refresh token travels in the HttpOnly cookie, so a user whose
// access token has expired can still sign out.

public sealed class Logout : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.Logout, Handle)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> Handle(
        HttpContext http,
        ISessionStore sessionStore,
        RefreshTokenCookie refreshCookie,
        SecurityAudit audit,
        CancellationToken ct)
    {
        byte[]? tokenHash = RefreshTokenCookie.Read(http.Request) is { } token ? OpaqueToken.Hash(token) : null;

        // Idempotent: a missing, unknown or already revoked cookie is not an error and writes no audit record.
        if (tokenHash is not null && await sessionStore.RevokeByTokenAsync(tokenHash, ct) is { } revoked)
        {
            await audit.WriteAsync(
                revoked.UserId,
                SecurityEventType.Logout,
                SecurityEventOutcome.Succeeded,
                ClientInfo.From(http) with { DeviceType = revoked.Device.DeviceType },
                ct);
        }

        refreshCookie.Delete(http.Response);

        return Results.NoContent();
    }
}
