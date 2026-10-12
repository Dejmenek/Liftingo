using FluentValidation;
using FluentValidation.Results;

using Liftingo.Application.Common.Exceptions;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

/// <param name="AcceptTermsAndAge">Required.</param>
/// <param name="AcceptHealthData">Required.</param>
/// <param name="AcceptMarketing">Optional.</param>
/// <param name="Email">Needed only when the provider gave no e-mail (<c>emailRequired</c> in the pending response).</param>
public sealed record CompleteExternalLoginRequest(
    bool AcceptTermsAndAge,
    bool AcceptHealthData,
    bool AcceptMarketing,
    string? Email);

// A new account or a takeover answers with the shared AuthSessionResponse (Shared/AuthSessionResponse.cs).

/// <param name="Status">Always <see cref="CompleteExternalLogin.LinkConfirmationSent"/>.</param>
public sealed record ExternalLoginLinkConfirmationResponse(string Status);

public sealed class CompleteExternalLoginValidator : AbstractValidator<CompleteExternalLoginRequest>
{
    public CompleteExternalLoginValidator()
    {
        RuleFor(r => r.AcceptTermsAndAge).MustAcceptTermsAndAge();
        RuleFor(r => r.AcceptHealthData).MustAcceptHealthData();

        // Whether the e-mail is required at all depends on the pending sign-in, so the handler checks that.
        When(r => !string.IsNullOrWhiteSpace(r.Email), () => RuleFor(r => r.Email!).MustBeValidEmail());
    }
}

public sealed class CompleteExternalLogin : IEndpoint
{
    public const string LinkConfirmationSent = "linkConfirmationSent";

    private static readonly CompleteExternalLoginValidator s_validator = new();

    private static readonly Error s_emailRequired =
        Error.Validation("Validation.EmailRequired", "Podaj adres e-mail.");

    private static readonly Error s_emailTaken =
        Error.Conflict("Auth.EmailTaken", "Konto z tym adresem e-mail już istnieje.");

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.CompleteExternalLogin, Handle)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<ExternalLoginLinkConfirmationResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> Handle(
        CompleteExternalLoginRequest request,
        HttpContext http,
        IIdentityService identity,
        IApplicationDbContext db,
        ConsentRecorder consents,
        SessionIssuer sessions,
        EmailTokenIssuer emailTokens,
        AccountTakeover takeover,
        AccountEmails emails,
        SecurityAudit audit,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        ValidationResult validation = await s_validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
        {
            return Result.Failure(validation.ToValidationError()).ToProblem();
        }

        PendingExternalLogin? pending = await PendingExternalLogin.ReadAsync(http);

        if (pending is null)
        {
            return Result.Failure(ExternalLoginErrors.Expired).ToProblem();
        }

        // A provider e-mail is treated as verified. An e-mail the user typed is not (FR-023, FR-024).
        bool providerEmail = pending.Email is not null;
        string? email = pending.Email ?? request.Email?.Trim();

        if (string.IsNullOrEmpty(email))
        {
            return Result.Failure(new ValidationError([s_emailRequired])).ToProblem();
        }

        ClientInfo client = ClientInfo.From(http, DeviceIdOf(http, pending));
        AccountSummary? existing = await identity.FindByEmailAsync(email, ct);

        if (existing is null)
        {
            return await CreateAccountAsync(
                request, http, pending, email, providerEmail, identity, db, consents, sessions, emailTokens, emails, timeProvider, client, ct);
        }

        // The provider vouches for the address, so the account is linked right away (R10).
        if (providerEmail)
        {
            return await LinkAccountAsync(
                request, http, pending, existing, identity, db, sessions, takeover, emails, audit, client, ct);
        }

        // The address was typed in: the owner has to confirm by e-mail before anything is linked (FR-023a, R17).
        return await RequestLinkConfirmationAsync(http, pending, existing, db, emailTokens, emails, ct);
    }

    private static async Task<IResult> CreateAccountAsync(
        CompleteExternalLoginRequest request,
        HttpContext http,
        PendingExternalLogin pending,
        string email,
        bool providerEmail,
        IIdentityService identity,
        IApplicationDbContext db,
        ConsentRecorder consents,
        SessionIssuer sessions,
        EmailTokenIssuer emailTokens,
        AccountEmails emails,
        TimeProvider timeProvider,
        ClientInfo client,
        CancellationToken ct)
    {
        // Nothing below saves until the single SaveChangesAsync, so a failure leaves no half-created account (SC-007).
        Result<Guid> created = await identity.CreateUserAsync(
            email,
            password: null,
            emailConfirmed: providerEmail,
            timeProvider.GetUtcNow(),
            ct);

        if (created.IsFailure)
        {
            return created.ToProblem();
        }

        Guid userId = created.Value;

        await identity.AddLoginAsync(userId, pending.Provider, pending.ProviderKey, pending.DisplayName, ct);
        consents.Stage(userId, request.AcceptTermsAndAge, request.AcceptHealthData, request.AcceptMarketing);
        await sessions.StageDeviceAsync(userId, client, SignInKind.Registration, ct);

        string? verificationToken = providerEmail
            ? null
            : await emailTokens.IssueAsync(userId, EmailTokenPurpose.EmailVerification, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // The address or the provider identity was taken between the lookup and the commit.
            return Result.Failure(s_emailTaken).ToProblem();
        }

        // SQL is committed; now Redis (session), then the e-mail (R4).
        AuthSessionResponse response = await sessions.IssueAsync(http, userId, client, ct);
        await PendingExternalLogin.ClearAsync(http);

        // A typed e-mail gets the same combined welcome and verification e-mail as registration (FR-025). A provider
        // e-mail gets the welcome-only e-mail instead (FR-025).
        if (verificationToken is not null)
        {
            await emails.SendVerificationAndWelcomeAsync(userId, email, verificationToken, ct);
        }
        else
        {
            await emails.SendWelcomeAsync(userId, email, ct);
        }

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> LinkAccountAsync(
        CompleteExternalLoginRequest request,
        HttpContext http,
        PendingExternalLogin pending,
        AccountSummary account,
        IIdentityService identity,
        IApplicationDbContext db,
        SessionIssuer sessions,
        AccountTakeover takeover,
        AccountEmails emails,
        SecurityAudit audit,
        ClientInfo client,
        CancellationToken ct)
    {
        if (await identity.GetLockoutEndAsync(account.Id, ct) is { } lockedUntil)
        {
            await audit.WriteAsync(account.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.LockedOut, client, ct);

            return Result.Failure(new LockedOutError(lockedUntil)).ToProblem();
        }

        // An unverified account is taken over from whoever registered it (R10); a verified one just gets the login.
        bool takeoverNeeded = !account.EmailVerified;

        await identity.AddLoginAsync(account.Id, pending.Provider, pending.ProviderKey, pending.DisplayName, ct);

        if (takeoverNeeded)
        {
            await takeover.StageAsync(
                account.Id,
                new TakeoverConsents(request.AcceptTermsAndAge, request.AcceptHealthData, request.AcceptMarketing),
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
        await PendingExternalLogin.ClearAsync(http);

        if (isNewDevice)
        {
            await emails.SendNewDeviceAsync(account.Id, account.Email, client.DeviceType, ct);
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> RequestLinkConfirmationAsync(
        HttpContext http,
        PendingExternalLogin pending,
        AccountSummary account,
        IApplicationDbContext db,
        EmailTokenIssuer emailTokens,
        AccountEmails emails,
        CancellationToken ct)
    {
        // The consents that came with the request are not recorded: the account has its own, and whoever opens the
        // link may be someone else. At the hourly limit the token is null: same answer, no e-mail (FR-037).
        string? token = await emailTokens.IssueAsync(
            account.Id,
            EmailTokenPurpose.ProviderLinkConfirmation,
            new ProviderLinkInfo(pending.Provider, pending.ProviderKey),
            ct);

        await db.SaveChangesAsync(ct);

        if (token is not null)
        {
            await emails.SendProviderLinkConfirmationAsync(account.Id, account.Email, pending.Provider, token, ct);
        }

        await PendingExternalLogin.ClearAsync(http);

        return Results.Json(
            new ExternalLoginLinkConfirmationResponse(LinkConfirmationSent),
            statusCode: StatusCodes.Status202Accepted);
    }

    // The SPA sends X-Device-Id on this call and it is the freshest; the id from the start of the flow is the fallback.
    private static Guid? DeviceIdOf(HttpContext http, PendingExternalLogin pending) =>
        Guid.TryParse(http.Request.Headers[ClientInfo.DeviceIdHeader].ToString(), out Guid fromHeader) && fromHeader != Guid.Empty
            ? fromHeader
            : pending.DeviceId;
}
