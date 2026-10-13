using FluentValidation.Results;

using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.ConfirmProviderLink;

/// <summary>
/// Links a provider to an existing account once its owner opens the confirmation link, and signs them in.
/// The token identifies the account.
/// </summary>
public sealed class ConfirmProviderLinkCommandHandler(
    ConfirmProviderLinkCommandValidator validator,
    IIdentityService identity,
    IApplicationDbContext db,
    EmailTokenIssuer emailTokens,
    AccountTakeover takeover,
    SessionIssuer sessions,
    IEmailSender emails,
    SecurityAudit audit,
    TimeProvider timeProvider) : ICommandHandler<ConfirmProviderLinkCommand, ConfirmProviderLinkResponse>
{
    private static readonly Error s_linkInvalid = Error.Validation(
        "Auth.ProviderLinkInvalid",
        "Link do potwierdzenia połączenia jest nieprawidłowy, wygasł, został już użyty albo zastąpiony nowszym. Zaloguj się przez dostawcę ponownie.");

    private static readonly Error s_alreadyLinked = Error.Conflict(
        "Auth.ProviderAlreadyLinked",
        "To logowanie jest już połączone z kontem. Zaloguj się przez dostawcę.");

    public async Task<Result<ConfirmProviderLinkResponse>> HandleAsync(ConfirmProviderLinkCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidationResult validation = await validator.ValidateAsync(command, ct);

        if (!validation.IsValid)
        {
            return Result.Failure<ConfirmProviderLinkResponse>(validation.ToValidationError());
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        EmailToken? token = await emailTokens.FindAsync(command.Token, EmailTokenPurpose.ProviderLinkConfirmation, ct);

        // 1. The token has to be usable.
        if (token is not { LoginProvider: { } loginProvider, ProviderKey: { } providerKey }
            || !token.IsUsable(now)
            || await identity.GetSummaryAsync(token.UserId, ct) is not { } account)
        {
            return Result.Failure<ConfirmProviderLinkResponse>(s_linkInvalid);
        }

        // 2. The user finished another way in the meantime: nothing to link, and the token is spent.
        if (await identity.FindByLoginAsync(loginProvider, providerKey, ct) is not null)
        {
            token.Consume(now);
            await db.SaveChangesAsync(ct);

            return Result.Failure<ConfirmProviderLinkResponse>(s_alreadyLinked);
        }

        ClientInfo client = command.Client;

        // A locked account refuses. Nothing changes, so the token stays usable and no failed attempt is counted.
        if (await identity.GetLockoutEndAsync(account.Id, ct) is { } lockedUntil)
        {
            await audit.WriteAsync(account.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.LockedOut, client, ct);

            return Result.Failure<ConfirmProviderLinkResponse>(new LockedOutError(lockedUntil));
        }

        // 3. Opening the link proves the inbox, which takes an unverified account over from whoever registered it.
        // The required consents come first; without them the token stays usable.
        bool takeoverNeeded = !account.EmailVerified;

        if (takeoverNeeded && command is not { AcceptTermsAndAge: true, AcceptHealthData: true })
        {
            return Result.Failure<ConfirmProviderLinkResponse>(ExternalLoginErrors.ConsentRequired);
        }

        // 4. Consume our own token first, so the takeover does not supersede it.
        token.Consume(now);

        await identity.AddLoginAsync(account.Id, loginProvider, providerKey, displayName: null, ct);

        if (takeoverNeeded)
        {
            await takeover.StageAsync(
                account.Id,
                new TakeoverConsents(AcceptTermsAndAge: true, AcceptHealthData: true, command.AcceptMarketing ?? false),
                PasswordMode.Remove,
                ct);
        }

        // A device the account has not used before gets a security notice once the session exists.
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

        return new ConfirmProviderLinkResponse(session);
    }
}
