using Liftingo.Application.Common;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Liftingo.Application.Features.Account;

// No request body and no response body. The signed-in user comes from the bearer token.

public sealed class LogoutAllDevices : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.LogoutAllDevices, Handle)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> Handle(
        HttpContext http,
        IUserContext userContext,
        IApplicationDbContext db,
        ISessionStore sessionStore,
        RefreshTokenCookie refreshCookie,
        SecurityAudit audit,
        CancellationToken ct)
    {
        Guid userId = userContext.UserId;

        // Forgetting the devices is the only SQL write here, so it needs no transaction. The next sign-in from any
        // device counts as new and sends the new-device e-mail.
        await db.KnownDevices.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);

        await sessionStore.RevokeAllAsync(userId, ct);

        await audit.WriteAsync(userId, SecurityEventType.LogoutAllDevices, SecurityEventOutcome.Succeeded, ClientInfo.From(http), ct);

        refreshCookie.Delete(http.Response);

        return Results.NoContent();
    }
}
