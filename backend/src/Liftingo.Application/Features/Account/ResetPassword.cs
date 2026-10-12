using FluentValidation;
using FluentValidation.Results;

using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Liftingo.Application.Features.Account;

/// <param name="Token">The token from the reset e-mail.</param>
/// <param name="NewPassword">The new password.</param>
/// <param name="AcceptTermsAndAge">Only read when the account e-mail is unverified (FR-035).</param>
/// <param name="AcceptHealthData">Only read when the account e-mail is unverified (FR-035).</param>
/// <param name="AcceptMarketing">Only read when the account e-mail is unverified (FR-035).</param>
public sealed record ResetPasswordRequest(
    string Token,
    string NewPassword,
    bool? AcceptTermsAndAge,
    bool? AcceptHealthData,
    bool? AcceptMarketing);

// No response body and no session: the user signs in with the new password (FR-035).

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(r => r.Token)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .WithErrorCode("Validation.TokenRequired")
            .WithMessage("Brakuje tokenu resetu hasła.");

        RuleFor(r => r.NewPassword).MustBeAcceptablePassword();
    }
}

public sealed class ResetPassword : IEndpoint
{
    private static readonly ResetPasswordValidator s_validator = new();

    private static readonly Error s_linkInvalid = Error.Validation(
        "Auth.ResetLinkInvalid",
        "Link do resetu hasła jest nieprawidłowy, wygasł, został już użyty albo zastąpiony nowszym. Poproś o nowy link.");

    private static readonly Error s_consentRequired = Error.Validation(
        "Auth.ConsentRequired",
        "Adres e-mail tego konta nie był jeszcze potwierdzony. Aby dokończyć reset hasła, potwierdź ponownie zgody: " +
        "ukończone 18 lat z akceptacją Regulaminu i Polityki prywatności oraz zgodę na przetwarzanie danych dotyczących zdrowia.");

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ResetPassword, Handle)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> Handle(
        ResetPasswordRequest request,
        HttpContext http,
        IIdentityService identity,
        IApplicationDbContext db,
        ISessionStore sessionStore,
        EmailTokenIssuer emailTokens,
        AccountTakeover takeover,
        RefreshTokenCookie refreshCookie,
        SecurityAudit audit,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        ValidationResult validation = await s_validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
        {
            return Result.Failure(validation.ToValidationError()).ToProblem();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        EmailToken? token = await emailTokens.FindAsync(request.Token, EmailTokenPurpose.PasswordReset, ct);

        if (token is null
            || !token.IsUsable(now)
            || await identity.GetSummaryAsync(token.UserId, ct) is not { } account)
        {
            return Result.Failure(s_linkInvalid).ToProblem();
        }

        Guid userId = account.Id;
        bool takeoverNeeded = !account.EmailVerified;

        // Opening the link proved the inbox, so for an unverified account this takes it over from whoever registered
        // it (FR-035, R7). The required consents come first: nothing is touched without them.
        if (takeoverNeeded && request is not { AcceptTermsAndAge: true, AcceptHealthData: true })
        {
            return Result.Failure(s_consentRequired).ToProblem();
        }

        // Stages the new hash and clears the lockout and the failed-attempt counter. A password that breaks the
        // policy stages nothing, so the token stays usable.
        Result reset = await identity.ResetPasswordAsync(userId, request.NewPassword, ct);

        if (reset.IsFailure)
        {
            return reset.ToProblem();
        }

        // SaveChangesAsync and ExecuteDeleteAsync are two statements that must succeed or fail together.
        await using (IDbContextTransaction transaction = await db.BeginTransactionAsync(ct))
        {
            token.Consume(now);

            if (takeoverNeeded)
            {
                await takeover.StageAsync(
                    userId,
                    new TakeoverConsents(AcceptTermsAndAge: true, AcceptHealthData: true, request.AcceptMarketing ?? false),
                    PasswordMode.Keep,
                    ct);
            }

            await db.SaveChangesAsync(ct);
            await db.KnownDevices.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
        }

        // SQL is committed; now Redis, the audit record and the cookie. Every session ends, the caller's included.
        await sessionStore.RevokeAllAsync(userId, ct);
        await audit.WriteAsync(userId, SecurityEventType.PasswordResetCompleted, SecurityEventOutcome.Succeeded, ClientInfo.From(http), ct);

        refreshCookie.Delete(http.Response);

        return Results.NoContent();
    }
}
