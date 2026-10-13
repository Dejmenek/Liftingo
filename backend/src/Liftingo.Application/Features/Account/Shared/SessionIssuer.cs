using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Security;
using Liftingo.Domain.Account;

using Microsoft.EntityFrameworkCore;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>
/// Starts a signed-in session in two steps around the handler's commit: SQL first, Redis second (R4).
/// It never calls <c>SaveChangesAsync</c>; the handler does, so the commit stays visible in the slice.
/// </summary>
public sealed class SessionIssuer(
    IApplicationDbContext db,
    ISessionStore sessions,
    IAccessTokenIssuer accessTokens,
    IIdentityService identity,
    TimeProvider timeProvider)
{
    /// <summary>Stages the <see cref="KnownDevice"/> upsert. Call it before <c>SaveChangesAsync</c>.</summary>
    /// <returns><c>true</c> when the device is new and the user should get the new-device e-mail.</returns>
    public async Task<bool> StageDeviceAsync(Guid userId, ClientInfo client, SignInKind kind, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (kind == SignInKind.Registration)
        {
            db.KnownDevices.Add(KnownDevice.Create(userId, client.DeviceId, client.DeviceType, now));

            return false;
        }

        KnownDevice? known = await db.KnownDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == client.DeviceId, ct);

        if (known is null)
        {
            db.KnownDevices.Add(KnownDevice.Create(userId, client.DeviceId, client.DeviceType, now));

            return true;
        }

        known.RecordSignIn(client.DeviceType, now);

        return false;
    }

    /// <summary>
    /// Creates the Redis session and the access token. Call it after the commit. Api writes the refresh token to
    /// the cookie.
    /// </summary>
    public async Task<IssuedSession> IssueAsync(Guid userId, ClientInfo client, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);

        AccountSummary summary = await identity.GetSummaryAsync(userId, ct)
            ?? throw new InvalidOperationException($"Account {userId} does not exist after it was committed.");

        (Guid sessionId, string refreshToken, TimeSpan remainingLifetime) =
            await sessions.CreateAsync(userId, client.ToSessionDevice(), ct);

        AccessToken accessToken = accessTokens.Issue(userId, sessionId, summary.EmailVerified);

        return new IssuedSession(accessToken.Value, accessToken.ExpiresAt, summary, refreshToken, remainingLifetime);
    }
}
