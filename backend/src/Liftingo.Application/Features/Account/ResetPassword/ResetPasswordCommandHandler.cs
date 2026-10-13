using FluentValidation.Results;

using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Liftingo.Application.Features.Account.ResetPassword;

/// <summary>
/// Sets a new password from a reset link and signs every session out (FR-034, FR-035). It starts no session: the
/// user signs in with the new password.
/// </summary>
public sealed class ResetPasswordCommandHandler(
    ResetPasswordCommandValidator validator,
    IIdentityService identity,
    IApplicationDbContext db,
    ISessionStore sessionStore,
    EmailTokenIssuer emailTokens,
    AccountTakeover takeover,
    SecurityAudit audit,
    TimeProvider timeProvider) : ICommandHandler<ResetPasswordCommand>
{
    private static readonly Error s_linkInvalid = Error.Validation(
        "Auth.ResetLinkInvalid",
        "Link do resetu hasła jest nieprawidłowy, wygasł, został już użyty albo zastąpiony nowszym. Poproś o nowy link.");

    private static readonly Error s_consentRequired = Error.Validation(
        "Auth.ConsentRequired",
        "Adres e-mail tego konta nie był jeszcze potwierdzony. Aby dokończyć reset hasła, potwierdź ponownie zgody: " +
        "ukończone 18 lat z akceptacją Regulaminu i Polityki prywatności oraz zgodę na przetwarzanie danych dotyczących zdrowia.");

    public async Task<Result> HandleAsync(ResetPasswordCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidationResult validation = await validator.ValidateAsync(command, ct);

        if (!validation.IsValid)
        {
            return Result.Failure(validation.ToValidationError());
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        EmailToken? token = await emailTokens.FindAsync(command.Token, EmailTokenPurpose.PasswordReset, ct);

        if (token is null
            || !token.IsUsable(now)
            || await identity.GetSummaryAsync(token.UserId, ct) is not { } account)
        {
            return Result.Failure(s_linkInvalid);
        }

        Guid userId = account.Id;
        bool takeoverNeeded = !account.EmailVerified;

        // Opening the link proved the inbox, so for an unverified account this takes it over from whoever registered
        // it (FR-035, R7). The required consents come first: nothing is touched without them.
        if (takeoverNeeded && command is not { AcceptTermsAndAge: true, AcceptHealthData: true })
        {
            return Result.Failure(s_consentRequired);
        }

        // Stages the new hash and clears the lockout and the failed-attempt counter. A password that breaks the
        // policy stages nothing, so the token stays usable.
        Result reset = await identity.ResetPasswordAsync(userId, command.NewPassword, ct);

        if (reset.IsFailure)
        {
            return reset;
        }

        // SaveChangesAsync and ExecuteDeleteAsync are two statements that must succeed or fail together.
        await using (IDbContextTransaction transaction = await db.BeginTransactionAsync(ct))
        {
            token.Consume(now);

            if (takeoverNeeded)
            {
                await takeover.StageAsync(
                    userId,
                    new TakeoverConsents(AcceptTermsAndAge: true, AcceptHealthData: true, command.AcceptMarketing ?? false),
                    PasswordMode.Keep,
                    ct);
            }

            await db.SaveChangesAsync(ct);
            await db.KnownDevices.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
        }

        // SQL is committed; now Redis and the audit record. Every session ends, the caller's included.
        await sessionStore.RevokeAllAsync(userId, ct);
        await audit.WriteAsync(userId, SecurityEventType.PasswordResetCompleted, SecurityEventOutcome.Succeeded, command.Client, ct);

        return Result.Success();
    }
}
