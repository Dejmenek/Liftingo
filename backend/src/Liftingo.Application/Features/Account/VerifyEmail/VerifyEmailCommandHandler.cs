using FluentValidation.Results;

using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.VerifyEmail;

/// <summary>
/// Marks the e-mail of the token's account as verified. The token identifies the account, so a different
/// signed-in user is ignored.
/// </summary>
public sealed class VerifyEmailCommandHandler(
    VerifyEmailCommandValidator validator,
    IIdentityService identity,
    IApplicationDbContext db,
    EmailTokenIssuer emailTokens,
    TimeProvider timeProvider) : ICommandHandler<VerifyEmailCommand, VerifyEmailResponse>
{
    private static readonly Error s_linkInvalid = Error.Validation(
        "Auth.VerificationLinkInvalid",
        "Link weryfikacyjny jest nieprawidłowy, wygasł albo został już zastąpiony nowszym. Poproś o nowy link.");

    public async Task<Result<VerifyEmailResponse>> HandleAsync(VerifyEmailCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidationResult validation = await validator.ValidateAsync(command, ct);

        if (!validation.IsValid)
        {
            return Result.Failure<VerifyEmailResponse>(validation.ToValidationError());
        }

        EmailToken? token = await emailTokens.FindAsync(command.Token, EmailTokenPurpose.EmailVerification, ct);

        if (token is null || await identity.GetSummaryAsync(token.UserId, ct) is not { } account)
        {
            return Result.Failure<VerifyEmailResponse>(s_linkInvalid);
        }

        // Whatever the token's state: a verified account (provider sign-in, password reset) reports success.
        if (account.EmailVerified)
        {
            return new VerifyEmailResponse(VerifyEmailResponse.AlreadyVerified);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (!token.IsUsable(now))
        {
            return Result.Failure<VerifyEmailResponse>(s_linkInvalid);
        }

        token.Consume(now);
        await identity.ConfirmEmailAsync(token.UserId, ct);
        await db.SaveChangesAsync(ct);

        return new VerifyEmailResponse(VerifyEmailResponse.Verified);
    }
}