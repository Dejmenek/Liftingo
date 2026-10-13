using FluentValidation;
using FluentValidation.Results;

using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

/// <param name="Token">The token from the link-confirmation e-mail.</param>
/// <param name="AcceptTermsAndAge">Only read when the account e-mail is unverified (R17).</param>
/// <param name="AcceptHealthData">Only read when the account e-mail is unverified (R17).</param>
/// <param name="AcceptMarketing">Only read when the account e-mail is unverified (R17).</param>
public sealed record ConfirmProviderLinkRequest(
    string Token,
    bool? AcceptTermsAndAge,
    bool? AcceptHealthData,
    bool? AcceptMarketing);

// The response is the shared AuthSessionResponse (Shared/AuthSessionResponse.cs).

public sealed class ConfirmProviderLinkValidator : AbstractValidator<ConfirmProviderLinkRequest>
{
    public ConfirmProviderLinkValidator()
    {
        RuleFor(r => r.Token)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .WithErrorCode("Validation.TokenRequired")
            .WithMessage("Brakuje tokenu potwierdzenia połączenia.");
    }
}

public sealed class ConfirmProviderLink : IEndpoint
{
    private static readonly ConfirmProviderLinkValidator s_validator = new();

    private static readonly Error s_linkInvalid = Error.Validation(
        "Auth.ProviderLinkInvalid",
        "Link do potwierdzenia połączenia jest nieprawidłowy, wygasł, został już użyty albo zastąpiony nowszym. Zaloguj się przez dostawcę ponownie.");

    private static readonly Error s_alreadyLinked = Error.Conflict(
        "Auth.ProviderAlreadyLinked",
        "To logowanie jest już połączone z kontem. Zaloguj się przez dostawcę.");

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ConfirmProviderLink, Handle)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    // Anonymous on purpose: the token identifies the account.
    private static async Task<IResult> Handle(
        ConfirmProviderLinkRequest request,
        HttpContext http,
        IIdentityService identity,
        IApplicationDbContext db,
        EmailTokenIssuer emailTokens,
        AccountTakeover takeover,
        SessionIssuer sessions,
        IEmailSender emails,
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
        EmailToken? token = await emailTokens.FindAsync(request.Token, EmailTokenPurpose.ProviderLinkConfirmation, ct);

        // 1. The token has to be usable (R17).
        if (token is not { LoginProvider: { } loginProvider, ProviderKey: { } providerKey }
            || !token.IsUsable(now)
            || await identity.GetSummaryAsync(token.UserId, ct) is not { } account)
        {
            return Result.Failure(s_linkInvalid).ToProblem();
        }

        // 2. The user finished another way in the meantime: nothing to link, and the token is spent.
        if (await identity.FindByLoginAsync(loginProvider, providerKey, ct) is not null)
        {
            token.Consume(now);
            await db.SaveChangesAsync(ct);

            return Result.Failure(s_alreadyLinked).ToProblem();
        }

        ClientInfo client = ClientInfo.From(http);

        // A locked account refuses. Nothing changes, so the token stays usable and no failed attempt is counted.
        if (await identity.GetLockoutEndAsync(account.Id, ct) is { } lockedUntil)
        {
            await audit.WriteAsync(account.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.LockedOut, client, ct);

            return Result.Failure(new LockedOutError(lockedUntil)).ToProblem();
        }

        // 3. Opening the link proves the inbox, which takes an unverified account over from whoever registered it
        // (R10). The required consents come first; without them the token stays usable.
        bool takeoverNeeded = !account.EmailVerified;

        if (takeoverNeeded && request is not { AcceptTermsAndAge: true, AcceptHealthData: true })
        {
            return Result.Failure(ExternalLoginErrors.ConsentRequired).ToProblem();
        }

        // 4. Consume our own token first, so the takeover does not supersede it.
        token.Consume(now);

        await identity.AddLoginAsync(account.Id, loginProvider, providerKey, displayName: null, ct);

        if (takeoverNeeded)
        {
            await takeover.StageAsync(
                account.Id,
                new TakeoverConsents(AcceptTermsAndAge: true, AcceptHealthData: true, request.AcceptMarketing ?? false),
                PasswordMode.Remove,
                ct);
        }

        // A device the account has not used before gets a security notice once the session exists (FR-026).
        bool isNewDevice = await sessions.StageDeviceAsync(account.Id, client, SignInKind.SignIn, ct);

        await db.SaveChangesAsync(ct);

        // SQL is committed; now Redis. The takeover ends every session first, so the new one is the only one left.
        if (takeoverNeeded)
        {
            await takeover.CompleteAsync(account.Id, ct);
        }

        AuthSessionResponse response = await sessions.IssueAsync(http, account.Id, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ProviderLinked, SecurityEventOutcome.Succeeded, client, ct);

        if (isNewDevice)
        {
            await emails.SendAsync(new NewDeviceEmail(account.Id, account.Email, timeProvider.GetUtcNow(), client.DeviceType), ct);
        }

        return Results.Ok(response);
    }
}
