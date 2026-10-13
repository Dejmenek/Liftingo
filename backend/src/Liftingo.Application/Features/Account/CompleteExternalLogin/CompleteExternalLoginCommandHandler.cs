using FluentValidation.Results;

using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Exceptions;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.CompleteExternalLogin;

/// <summary>
/// The consent step of a provider sign-in (FR-021): creates the account, takes over an unverified one, or asks the
/// owner of a typed-in address to confirm the link by e-mail (the contract's <c>complete</c> table).
/// </summary>
public sealed class CompleteExternalLoginCommandHandler(
    CompleteExternalLoginCommandValidator validator,
    IIdentityService identity,
    IApplicationDbContext db,
    ConsentRecorder consents,
    SessionIssuer sessions,
    EmailTokenIssuer emailTokens,
    AccountTakeover takeover,
    IEmailSender emails,
    SecurityAudit audit,
    TimeProvider timeProvider)
{
    private static readonly Error s_emailRequired =
        Error.Validation("Validation.EmailRequired", "Podaj adres e-mail.");

    private static readonly Error s_emailTaken =
        Error.Conflict("Auth.EmailTaken", "Konto z tym adresem e-mail już istnieje.");

    private static readonly CompleteExternalLoginResponse s_linkConfirmationSent =
        new(CompleteExternalLoginOutcome.LinkConfirmationSent, Session: null);

    public async Task<Result<CompleteExternalLoginResponse>> HandleAsync(CompleteExternalLoginCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidationResult validation = await validator.ValidateAsync(command, ct);

        if (!validation.IsValid)
        {
            return Result.Failure<CompleteExternalLoginResponse>(validation.ToValidationError());
        }

        if (command.External is not { } external)
        {
            return Result.Failure<CompleteExternalLoginResponse>(ExternalLoginErrors.Expired);
        }

        // A provider e-mail is treated as verified. An e-mail the user typed is not (FR-023, FR-024).
        bool providerEmail = external.Email is not null;
        string? email = external.Email ?? command.Email?.Trim();

        if (string.IsNullOrEmpty(email))
        {
            return Result.Failure<CompleteExternalLoginResponse>(new ValidationError([s_emailRequired]));
        }

        AccountSummary? existing = await identity.FindByEmailAsync(email, ct);

        if (existing is null)
        {
            return await CreateAccountAsync(command, external, email, providerEmail, ct);
        }

        // The provider vouches for the address, so the account is linked right away (R10).
        if (providerEmail)
        {
            return await LinkAccountAsync(command, external, existing, ct);
        }

        // The address was typed in: the owner has to confirm by e-mail before anything is linked (FR-023a, R17).
        return await RequestLinkConfirmationAsync(external, existing, ct);
    }

    private async Task<Result<CompleteExternalLoginResponse>> CreateAccountAsync(
        CompleteExternalLoginCommand command,
        ExternalIdentity external,
        string email,
        bool providerEmail,
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
            return Result.Failure<CompleteExternalLoginResponse>(created.Error);
        }

        Guid userId = created.Value;

        await identity.AddLoginAsync(userId, external.Provider, external.ProviderKey, external.DisplayName, ct);
        consents.Stage(userId, command.AcceptTermsAndAge, command.AcceptHealthData, command.AcceptMarketing);
        await sessions.StageDeviceAsync(userId, command.Client, SignInKind.Registration, ct);

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
            return Result.Failure<CompleteExternalLoginResponse>(s_emailTaken);
        }

        // SQL is committed; now Redis (session), then the e-mail (R4, FR-029).
        IssuedSession session = await sessions.IssueAsync(userId, command.Client, ct);

        // A typed e-mail gets the same combined welcome and verification e-mail as registration (FR-025). A provider
        // e-mail gets the welcome-only e-mail instead (FR-025).
        OutgoingEmail welcome = verificationToken is not null
            ? new VerificationAndWelcomeEmail(userId, email, verificationToken)
            : new WelcomeEmail(userId, email);

        await emails.SendAsync(welcome, ct);

        return new CompleteExternalLoginResponse(CompleteExternalLoginOutcome.AccountCreated, session);
    }

    private async Task<Result<CompleteExternalLoginResponse>> LinkAccountAsync(
        CompleteExternalLoginCommand command,
        ExternalIdentity external,
        AccountSummary account,
        CancellationToken ct)
    {
        ClientInfo client = command.Client;

        if (await identity.GetLockoutEndAsync(account.Id, ct) is { } lockedUntil)
        {
            await audit.WriteAsync(account.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.LockedOut, client, ct);

            return Result.Failure<CompleteExternalLoginResponse>(new LockedOutError(lockedUntil));
        }

        // An unverified account is taken over from whoever registered it (R10); a verified one just gets the login.
        bool takeoverNeeded = !account.EmailVerified;

        await identity.AddLoginAsync(account.Id, external.Provider, external.ProviderKey, external.DisplayName, ct);

        if (takeoverNeeded)
        {
            await takeover.StageAsync(
                account.Id,
                new TakeoverConsents(command.AcceptTermsAndAge, command.AcceptHealthData, command.AcceptMarketing),
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

        IssuedSession session = await sessions.IssueAsync(account.Id, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ProviderLinked, SecurityEventOutcome.Succeeded, client, ct);

        if (isNewDevice)
        {
            await emails.SendAsync(new NewDeviceEmail(account.Id, account.Email, timeProvider.GetUtcNow(), client.DeviceType), ct);
        }

        return new CompleteExternalLoginResponse(CompleteExternalLoginOutcome.SignedIn, session);
    }

    private async Task<Result<CompleteExternalLoginResponse>> RequestLinkConfirmationAsync(
        ExternalIdentity external,
        AccountSummary account,
        CancellationToken ct)
    {
        // The consents that came with the request are not recorded: the account has its own, and whoever opens the
        // link may be someone else. At the hourly limit the token is null: same answer, no e-mail (FR-037).
        string? token = await emailTokens.IssueAsync(
            account.Id,
            EmailTokenPurpose.ProviderLinkConfirmation,
            new ProviderLinkInfo(external.Provider, external.ProviderKey),
            ct);

        await db.SaveChangesAsync(ct);

        if (token is not null)
        {
            await emails.SendAsync(new ProviderLinkConfirmationEmail(account.Id, account.Email, external.Provider, token), ct);
        }

        return s_linkConfirmationSent;
    }
}
