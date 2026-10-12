using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

// No request body and no response body: always a 302 to the frontend, whatever happened (contract: callback table).

public sealed class HandleExternalLoginCallback : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.ExternalLoginCallback, Handle)
            .AllowAnonymous()
            .Produces(StatusCodes.Status302Found);

    private static async Task<IResult> Handle(
        HttpContext http,
        IIdentityService identity,
        IApplicationDbContext db,
        SessionIssuer sessions,
        AccountEmails emails,
        SecurityAudit audit,
        FrontendRedirects redirects,
        CancellationToken ct)
    {
        PendingExternalLogin? pending = await PendingExternalLogin.ReadAsync(http);

        if (pending is null)
        {
            // The provider failed or the user cancelled there (OnRemoteFailure sends the browser here), or the cookie
            // is missing, expired or does not validate. No user, so the audit record carries no IP and no device (FR-020).
            await audit.WriteAsync(null, SecurityEventType.ExternalLogin, SecurityEventOutcome.Failed, ClientInfo.From(http), ct);
            await PendingExternalLogin.ClearAsync(http);

            return redirects.External();
        }

        ClientInfo client = ClientInfo.From(http, pending.DeviceId);

        AccountSummary? linked = await identity.FindByLoginAsync(pending.Provider, pending.ProviderKey, ct);
        AccountSummary? emailOwner = pending.Email is null ? null : await identity.FindByEmailAsync(pending.Email, ct);

        if (linked is not null)
        {
            return await SignInLinkedAccountAsync(http, linked, emailOwner, identity, db, sessions, emails, audit, redirects, client, ct);
        }

        if (emailOwner is { EmailVerified: true })
        {
            return await LinkVerifiedAccountAsync(http, emailOwner, pending, identity, db, sessions, emails, audit, redirects, client, ct);
        }

        // A new user, or the owner of an unverified address taking the account over. Both need consents first
        // (FR-021, R10), and nothing is written until they give them: the cookie stays for the next step.
        return redirects.Consent();
    }

    private static async Task<IResult> SignInLinkedAccountAsync(
        HttpContext http,
        AccountSummary linked,
        AccountSummary? emailOwner,
        IIdentityService identity,
        IApplicationDbContext db,
        SessionIssuer sessions,
        AccountEmails emails,
        SecurityAudit audit,
        FrontendRedirects redirects,
        ClientInfo client,
        CancellationToken ct)
    {
        // The provider now reports an address that belongs to another account (FR-022a). Nobody is signed in and
        // neither account changes. A provider e-mail that belongs to nobody is ignored.
        if (emailOwner is not null && emailOwner.Id != linked.Id)
        {
            await audit.WriteAsync(linked.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.Failed, client, ct);
            await PendingExternalLogin.ClearAsync(http);

            return redirects.EmailConflict();
        }

        if (await RefuseIfLockedOutAsync(http, linked.Id, identity, audit, redirects, client, ct) is { } refused)
        {
            return refused;
        }

        // A device the account has not used before gets a security notice once the session exists (FR-026).
        bool isNewDevice = await sessions.StageDeviceAsync(linked.Id, client, SignInKind.SignIn, ct);

        await db.SaveChangesAsync(ct);

        await sessions.IssueAsync(http, linked.Id, client, ct);
        await audit.WriteAsync(linked.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.Succeeded, client, ct);
        await PendingExternalLogin.ClearAsync(http);

        if (isNewDevice)
        {
            await emails.SendNewDeviceAsync(linked.Id, linked.Email, client.DeviceType, ct);
        }

        return redirects.Callback();
    }

    private static async Task<IResult> LinkVerifiedAccountAsync(
        HttpContext http,
        AccountSummary account,
        PendingExternalLogin pending,
        IIdentityService identity,
        IApplicationDbContext db,
        SessionIssuer sessions,
        AccountEmails emails,
        SecurityAudit audit,
        FrontendRedirects redirects,
        ClientInfo client,
        CancellationToken ct)
    {
        // The address was proven by whoever verified it, and the provider vouches for it too (R10): link without
        // a consent step. A locked account refuses, and that never counts as a failed attempt.
        if (await RefuseIfLockedOutAsync(http, account.Id, identity, audit, redirects, client, ct) is { } refused)
        {
            return refused;
        }

        await identity.AddLoginAsync(account.Id, pending.Provider, pending.ProviderKey, pending.DisplayName, ct);

        // A device the account has not used before gets a security notice once the session exists (FR-026).
        bool isNewDevice = await sessions.StageDeviceAsync(account.Id, client, SignInKind.SignIn, ct);

        await db.SaveChangesAsync(ct);

        await sessions.IssueAsync(http, account.Id, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ProviderLinked, SecurityEventOutcome.Succeeded, client, ct);
        await audit.WriteAsync(account.Id, SecurityEventType.ExternalLogin, SecurityEventOutcome.Succeeded, client, ct);
        await PendingExternalLogin.ClearAsync(http);

        if (isNewDevice)
        {
            await emails.SendNewDeviceAsync(account.Id, account.Email, client.DeviceType, ct);
        }

        return redirects.Callback();
    }

    private static async Task<IResult?> RefuseIfLockedOutAsync(
        HttpContext http,
        Guid userId,
        IIdentityService identity,
        SecurityAudit audit,
        FrontendRedirects redirects,
        ClientInfo client,
        CancellationToken ct)
    {
        if (await identity.GetLockoutEndAsync(userId, ct) is not { } lockedUntil)
        {
            return null;
        }

        await audit.WriteAsync(userId, SecurityEventType.ExternalLogin, SecurityEventOutcome.LockedOut, client, ct);
        await PendingExternalLogin.ClearAsync(http);

        return redirects.LockedOut(lockedUntil);
    }
}
