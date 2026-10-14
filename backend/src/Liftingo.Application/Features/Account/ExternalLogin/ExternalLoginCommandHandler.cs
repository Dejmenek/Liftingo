using FluentValidation.Results;

using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Exceptions;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.ExternalLogin;

/// <summary>
/// Signs in with a credential the client got from Google or Facebook. After the provider confirms it, the
/// provider identity leads to one of: an account it is already linked to, a verified account with its e-mail, an
/// unverified account with its e-mail (taken over), or no account (created). Creating or taking over needs both
/// required consents; without them nothing is written and the client sends the same token again with them.
/// </summary>
public sealed class ExternalLoginCommandHandler(
    ExternalLoginCommandValidator validator,
    IExternalProviderCatalog catalog,
    IExternalIdentityVerifier verifier,
    IIdentityService identity,
    IApplicationDbContext db,
    ConsentRecorder consents,
    SessionIssuer sessions,
    EmailTokenIssuer emailTokens,
    AccountTakeover takeover,
    IEmailSender emails,
    SecurityAudit audit,
    TimeProvider timeProvider) : ICommandHandler<ExternalLoginCommand, ExternalLoginResponse>
{
    private static readonly Error s_emailRequired =
        Error.Validation("Validation.EmailRequired", "Podaj adres e-mail.");

    private static readonly Error s_emailTaken =
        Error.Conflict("Auth.EmailTaken", "Konto z tym adresem e-mail już istnieje.");

    private static readonly ExternalLoginResponse s_linkConfirmationSent =
        new(ExternalLoginOutcome.LinkConfirmationSent, Session: null);

    public async Task<Result<ExternalLoginResponse>> HandleAsync(ExternalLoginCommand command, CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(command, ct);

        if (!validation.IsValid)
        {
            return Result.Failure<ExternalLoginResponse>(validation.ToValidationError());
        }

        if (catalog.Find(command.Provider) is not { } provider)
        {
            return Result.Failure<ExternalLoginResponse>(ExternalLoginErrors.ProviderUnavailable);
        }

        ClientInfo client = command.Client;

        if (await verifier.VerifyAsync(provider.LoginProvider, command.Token, ct) is not { } external)
        {
            // Nobody is identified, so the audit record carries no IP and no device.
            await audit.WriteAsync(null, SecurityEventType.ExternalLogin, SecurityEventOutcome.Failed, client, ct);

            return Result.Failure<ExternalLoginResponse>(ExternalLoginErrors.Failed(provider.LoginProvider));
        }

        AccountSummary? linked = await identity.FindByLoginAsync(external.Provider, external.ProviderKey, ct);
        AccountSummary? emailOwner = external.Email is null ? null : await identity.FindByEmailAsync(external.Email, ct);

        // An account that already has its own consents: the ones in the request are ignored, not recorded.
        if (linked is not null)
        {
            return await SignInLinkedAccountAsync(linked, emailOwner, client, ct);
        }

        if (emailOwner is { EmailVerified: true })
        {
            return await LinkVerifiedAccountAsync(emailOwner, external, client, ct);
        }

        bool consentsGiven = command is { AcceptTermsAndAge: true, AcceptHealthData: true };

        // The provider proves the inbox of an address someone else registered and never verified. Consents come
        // before the takeover, so an owner who walks away at that step leaves the account as it was.
        if (emailOwner is not null)
        {
            if (await RefuseIfLockedOutAsync(emailOwner.Id, client, ct) is { } refused)
            {
                return Result.Failure<ExternalLoginResponse>(refused);
            }

            return consentsGiven
                ? await TakeOverAccountAsync(command, external, emailOwner, ct)
                : Result.Failure<ExternalLoginResponse>(new ConsentRequiredError(accountExists: true, emailRequired: false));
        }

        string? typedEmail = string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim();

        if (!consentsGiven)
        {
            return Result.Failure<ExternalLoginResponse>(new ConsentRequiredError(
                accountExists: false,
                emailRequired: external.Email is null && typedEmail is null));
        }

        // A provider e-mail is treated as verified.
        if (external.Email is not null)
        {
            return await CreateAccountAsync(command, external, external.Email, providerEmail: true, ct);
        }

        if (typedEmail is null)
        {
            return Result.Failure<ExternalLoginResponse>(new ValidationError([s_emailRequired]));
        }

        // A typed e-mail is not verified. If it already has an account, its owner has to confirm by e-mail before
        // anything is linked.
        return await identity.FindByEmailAsync(typedEmail, ct) is { } typedOwner
            ? await RequestLinkConfirmationAsync(external, typedOwner, ct)
            : await CreateAccountAsync(command, external, typedEmail, providerEmail: false, ct);
    }

    private async Task<Result<ExternalLoginResponse>> SignInLinkedAccountAsync(
        AccountSummary linked,
        AccountSummary? emailOwner,
        ClientInfo client,
        CancellationToken ct)
    {
        // The provider now reports an address that belongs to another account. Nobody is signed in and
        // neither account changes. A provider e-mail that belongs to nobody is ignored.
        if (emailOwner is not null && emailOwner.Id != linked.Id)
        {
            await audit.WriteAsync(linked.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.Failed, client, ct);

            return Result.Failure<ExternalLoginResponse>(ExternalLoginErrors.ProviderEmailConflict);
        }

        if (await RefuseIfLockedOutAsync(linked.Id, client, ct) is { } refused)
        {
            return Result.Failure<ExternalLoginResponse>(refused);
        }

        // A device the account has not used before gets a security notice once the session exists.
        bool isNewDevice = await sessions.StageDeviceAsync(linked.Id, client, SignInKind.SignIn, ct);

        await db.SaveChangesAsync(ct);

        IssuedSession session = await sessions.IssueAsync(linked.Id, client, ct);
        await audit.WriteAsync(linked.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.Succeeded, client, ct);

        await SendNewDeviceEmailAsync(isNewDevice, linked, client, ct);

        return new ExternalLoginResponse(ExternalLoginOutcome.SignedIn, session);
    }

    private async Task<Result<ExternalLoginResponse>> LinkVerifiedAccountAsync(
        AccountSummary account,
        ExternalIdentity external,
        ClientInfo client,
        CancellationToken ct)
    {
        // The address was proven by whoever verified it, and the provider vouches for it too: link without
        // a consent step.
        if (await RefuseIfLockedOutAsync(account.Id, client, ct) is { } refused)
        {
            return Result.Failure<ExternalLoginResponse>(refused);
        }

        await identity.AddLoginAsync(account.Id, external.Provider, external.ProviderKey, external.DisplayName, ct);

        bool isNewDevice = await sessions.StageDeviceAsync(account.Id, client, SignInKind.SignIn, ct);

        if (await SaveAsync(ct) is { } conflict)
        {
            return Result.Failure<ExternalLoginResponse>(conflict);
        }

        IssuedSession session = await sessions.IssueAsync(account.Id, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ProviderLinked, SecurityEventOutcome.Succeeded, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.Succeeded, client, ct);

        await SendNewDeviceEmailAsync(isNewDevice, account, client, ct);

        return new ExternalLoginResponse(ExternalLoginOutcome.SignedIn, session);
    }

    private async Task<Result<ExternalLoginResponse>> TakeOverAccountAsync(
        ExternalLoginCommand command,
        ExternalIdentity external,
        AccountSummary account,
        CancellationToken ct)
    {
        ClientInfo client = command.Client;

        await identity.AddLoginAsync(account.Id, external.Provider, external.ProviderKey, external.DisplayName, ct);
        await takeover.StageAsync(
            account.Id,
            new TakeoverConsents(AcceptTermsAndAge: true, AcceptHealthData: true, command.AcceptMarketing ?? false),
            PasswordMode.Remove,
            ct);

        bool isNewDevice = await sessions.StageDeviceAsync(account.Id, client, SignInKind.SignIn, ct);

        if (await SaveAsync(ct) is { } conflict)
        {
            return Result.Failure<ExternalLoginResponse>(conflict);
        }

        // SQL is committed; now Redis. The takeover ends every session first, so the new one is the only one left.
        await takeover.CompleteAsync(account.Id, ct);

        IssuedSession session = await sessions.IssueAsync(account.Id, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ProviderLinked, SecurityEventOutcome.Succeeded, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.Succeeded, client, ct);

        await SendNewDeviceEmailAsync(isNewDevice, account, client, ct);

        return new ExternalLoginResponse(ExternalLoginOutcome.SignedIn, session);
    }

    private async Task<Result<ExternalLoginResponse>> CreateAccountAsync(
        ExternalLoginCommand command,
        ExternalIdentity external,
        string email,
        bool providerEmail,
        CancellationToken ct)
    {
        // Nothing below saves until the single SaveChangesAsync, so a failure leaves no half-created account.
        Result<Guid> created = await identity.CreateUserAsync(
            email,
            password: null,
            emailConfirmed: providerEmail,
            timeProvider.GetUtcNow(),
            ct);

        if (created.IsFailure)
        {
            return Result.Failure<ExternalLoginResponse>(created.Error);
        }

        Guid userId = created.Value;

        await identity.AddLoginAsync(userId, external.Provider, external.ProviderKey, external.DisplayName, ct);
        consents.Stage(userId, acceptTermsAndAge: true, acceptHealthData: true, command.AcceptMarketing ?? false);
        await sessions.StageDeviceAsync(userId, command.Client, SignInKind.Registration, ct);

        string? verificationToken = providerEmail
            ? null
            : await emailTokens.IssueAsync(userId, EmailTokenPurpose.EmailVerification, ct);

        if (await SaveAsync(ct) is { } conflict)
        {
            return Result.Failure<ExternalLoginResponse>(conflict);
        }

        // SQL is committed; now Redis (session), the audit record, then the e-mail. Signing up through a provider
        // is still a provider sign-in, so it is audited like one.
        IssuedSession session = await sessions.IssueAsync(userId, command.Client, ct);
        await audit.WriteAsync(userId, SecurityEventType.ExternalLogin, SecurityEventOutcome.Succeeded, command.Client, ct);

        // A typed e-mail gets the same combined welcome and verification e-mail as registration. A provider
        // e-mail needs no verification, so it gets the welcome alone.
        OutgoingEmail welcome = verificationToken is not null
            ? new VerificationAndWelcomeEmail(userId, email, verificationToken)
            : new WelcomeEmail(userId, email);

        await emails.SendAsync(welcome, ct);

        return new ExternalLoginResponse(ExternalLoginOutcome.AccountCreated, session);
    }

    private async Task<Result<ExternalLoginResponse>> RequestLinkConfirmationAsync(
        ExternalIdentity external,
        AccountSummary account,
        CancellationToken ct)
    {
        // The consents that came with the request are not recorded: the account has its own, and whoever opens the
        // link may be someone else. At the hourly limit the token is null: same answer, no e-mail.
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

    // A locked account refuses every sign-in, and the refusal never counts as a failed attempt.
    private async Task<LockedOutError?> RefuseIfLockedOutAsync(Guid userId, ClientInfo client, CancellationToken ct)
    {
        if (await identity.GetLockoutEndAsync(userId, ct) is not { } lockedUntil)
        {
            return null;
        }

        await audit.WriteAsync(userId, SecurityEventType.ExternalLogin, SecurityEventOutcome.LockedOut, client, ct);

        return new LockedOutError(lockedUntil);
    }

    // The address or the provider identity can be taken between the lookups above and the commit, when two
    // sign-ins race. The unique indexes catch it, and the loser gets a conflict instead of a 500.
    private async Task<Error?> SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);

            return null;
        }
        catch (UniqueConstraintViolationException)
        {
            return s_emailTaken;
        }
    }

    private async Task SendNewDeviceEmailAsync(bool isNewDevice, AccountSummary account, ClientInfo client, CancellationToken ct)
    {
        if (isNewDevice)
        {
            await emails.SendAsync(new NewDeviceEmail(account.Id, account.Email, timeProvider.GetUtcNow(), client.DeviceType), ct);
        }
    }
}