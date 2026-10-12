using Liftingo.Application.Common;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

// No request body and no response body. The signed-in user comes from the bearer token.

public sealed class ResendVerificationEmail : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ResendVerificationEmail, Handle)
            .RequireAuthorization()
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> Handle(
        IUserContext userContext,
        IIdentityService identity,
        IApplicationDbContext db,
        EmailTokenIssuer emailTokens,
        AccountEmails emails,
        CancellationToken ct)
    {
        Guid userId = userContext.UserId;

        // 202 whatever happens, so the answer says nothing about the limit or the account state (FR-037).
        if (await identity.GetSummaryAsync(userId, ct) is { EmailVerified: false } account
            && await emailTokens.IssueAsync(userId, EmailTokenPurpose.EmailVerification, ct) is { } token)
        {
            await db.SaveChangesAsync(ct);
            await emails.SendVerificationAsync(userId, account.Email, token, ct);
        }

        return Results.Accepted();
    }
}
