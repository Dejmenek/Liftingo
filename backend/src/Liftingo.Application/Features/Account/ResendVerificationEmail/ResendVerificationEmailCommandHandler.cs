using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common;
using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.ResendVerificationEmail;

public sealed class ResendVerificationEmailCommandHandler(
    IUserContext userContext,
    IIdentityService identity,
    IApplicationDbContext db,
    EmailTokenIssuer emailTokens,
    IEmailSender emails) : ICommandHandler<ResendVerificationEmailCommand>
{
    public async Task<Result> HandleAsync(ResendVerificationEmailCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        Guid userId = userContext.UserId;

        // Success whatever happens, so the answer says nothing about the limit or the account state.
        if (await identity.GetSummaryAsync(userId, ct) is { EmailVerified: false } account
            && await emailTokens.IssueAsync(userId, EmailTokenPurpose.EmailVerification, ct) is { } token)
        {
            await db.SaveChangesAsync(ct);
            await emails.SendAsync(new VerificationEmail(userId, account.Email, token), ct);
        }

        return Result.Success();
    }
}
