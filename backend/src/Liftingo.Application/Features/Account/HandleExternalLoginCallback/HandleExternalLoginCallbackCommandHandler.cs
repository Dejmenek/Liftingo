using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.HandleExternalLoginCallback;

/// <summary>
/// Decides what happens after the provider sends the browser back. Refusals are
/// errors: <see cref="ExternalLoginErrors.Failed"/>, <see cref="ExternalLoginErrors.ProviderEmailConflict"/> and
/// <see cref="LockedOutError"/>. Api turns every outcome into a redirect.
/// </summary>
public sealed class HandleExternalLoginCallbackCommandHandler(
    IIdentityService identity,
    IApplicationDbContext db,
    SessionIssuer sessions,
    IEmailSender emails,
    SecurityAudit audit,
    TimeProvider timeProvider) : ICommandHandler<HandleExternalLoginCallbackCommand, HandleExternalLoginCallbackResponse>
{
    private static readonly HandleExternalLoginCallbackResponse s_consentRequired =
        new(HandleExternalLoginCallbackOutcome.ConsentRequired, Session: null);

    public async Task<Result<HandleExternalLoginCallbackResponse>> HandleAsync(
        HandleExternalLoginCallbackCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ClientInfo client = command.Client;

        if (command.External is not { } external)
        {
            // The provider failed or the user cancelled there (OnRemoteFailure sends the browser here), or the cookie
            // is missing, expired or does not validate. No user, so the audit record carries no IP and no device.
            await audit.WriteAsync(null, SecurityEventType.ExternalLogin, SecurityEventOutcome.Failed, client, ct);

            return Result.Failure<HandleExternalLoginCallbackResponse>(ExternalLoginErrors.Failed);
        }

        AccountSummary? linked = await identity.FindByLoginAsync(external.Provider, external.ProviderKey, ct);
        AccountSummary? emailOwner = external.Email is null ? null : await identity.FindByEmailAsync(external.Email, ct);

        if (linked is not null)
        {
            return await SignInLinkedAccountAsync(linked, emailOwner, client, ct);
        }

        if (emailOwner is { EmailVerified: true })
        {
            return await LinkVerifiedAccountAsync(emailOwner, external, client, ct);
        }

        // A new user, or the owner of an unverified address taking the account over. Both need consents first,
        // and nothing is written until they give them.
        return s_consentRequired;
    }

    private async Task<Result<HandleExternalLoginCallbackResponse>> SignInLinkedAccountAsync(
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

            return Result.Failure<HandleExternalLoginCallbackResponse>(ExternalLoginErrors.ProviderEmailConflict);
        }

        if (await RefuseIfLockedOutAsync(linked.Id, client, ct) is { } refused)
        {
            return Result.Failure<HandleExternalLoginCallbackResponse>(refused);
        }

        // A device the account has not used before gets a security notice once the session exists.
        bool isNewDevice = await sessions.StageDeviceAsync(linked.Id, client, SignInKind.SignIn, ct);

        await db.SaveChangesAsync(ct);

        IssuedSession session = await sessions.IssueAsync(linked.Id, client, ct);
        await audit.WriteAsync(linked.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.Succeeded, client, ct);

        if (isNewDevice)
        {
            await emails.SendAsync(new NewDeviceEmail(linked.Id, linked.Email, timeProvider.GetUtcNow(), client.DeviceType), ct);
        }

        return new HandleExternalLoginCallbackResponse(HandleExternalLoginCallbackOutcome.SignedIn, session);
    }

    private async Task<Result<HandleExternalLoginCallbackResponse>> LinkVerifiedAccountAsync(
        AccountSummary account,
        ExternalIdentity external,
        ClientInfo client,
        CancellationToken ct)
    {
        // The address was proven by whoever verified it, and the provider vouches for it too: link without
        // a consent step. A locked account refuses, and that never counts as a failed attempt.
        if (await RefuseIfLockedOutAsync(account.Id, client, ct) is { } refused)
        {
            return Result.Failure<HandleExternalLoginCallbackResponse>(refused);
        }

        await identity.AddLoginAsync(account.Id, external.Provider, external.ProviderKey, external.DisplayName, ct);

        // A device the account has not used before gets a security notice once the session exists.
        bool isNewDevice = await sessions.StageDeviceAsync(account.Id, client, SignInKind.SignIn, ct);

        await db.SaveChangesAsync(ct);

        IssuedSession session = await sessions.IssueAsync(account.Id, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ProviderLinked, SecurityEventOutcome.Succeeded, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.Succeeded, client, ct);

        if (isNewDevice)
        {
            await emails.SendAsync(new NewDeviceEmail(account.Id, account.Email, timeProvider.GetUtcNow(), client.DeviceType), ct);
        }

        return new HandleExternalLoginCallbackResponse(HandleExternalLoginCallbackOutcome.SignedIn, session);
    }

    private async Task<LockedOutError?> RefuseIfLockedOutAsync(Guid userId, ClientInfo client, CancellationToken ct)
    {
        if (await identity.GetLockoutEndAsync(userId, ct) is not { } lockedUntil)
        {
            return null;
        }

        await audit.WriteAsync(userId, SecurityEventType.ExternalLogin, SecurityEventOutcome.LockedOut, client, ct);

        return new LockedOutError(lockedUntil);
    }
}
