using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Security;
using Liftingo.Domain.Account;

using Microsoft.EntityFrameworkCore;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>What happens to the password when an unverified account is taken over.</summary>
public enum PasswordMode
{
    // Provider takeovers: the squatter's password goes.
    Remove,

    // Password reset: the owner just chose a new password.
    Keep,
}

/// <summary>The consents the real owner gave when taking over the account.</summary>
public sealed record TakeoverConsents(bool AcceptTermsAndAge, bool AcceptHealthData, bool AcceptMarketing);

/// <summary>
/// The owner of an unverified e-mail address proves the inbox (provider sign-in, link confirmation or password
/// reset) and takes the account over from whoever registered it (R10, R7).
/// </summary>
public sealed class AccountTakeover(
    IIdentityService identity,
    ConsentRecorder consents,
    IApplicationDbContext db,
    ISessionStore sessions,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Stages the takeover and does not save. Callers consume their own token <b>before</b> calling this, so it
    /// is no longer usable and is skipped when the others are superseded.
    /// </summary>
    public async Task StageAsync(Guid userId, TakeoverConsents newConsents, PasswordMode mode, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(newConsents);

        DateTimeOffset now = timeProvider.GetUtcNow();

        await identity.ConfirmEmailAsync(userId, ct);

        if (mode == PasswordMode.Remove)
        {
            await identity.RemovePasswordAsync(userId, ct);
        }

        await consents.SupersedeCurrentAsync(userId, ct);
        consents.Stage(userId, newConsents.AcceptTermsAndAge, newConsents.AcceptHealthData, newConsents.AcceptMarketing);

        List<EmailToken> open = await db.EmailTokens
            .Where(t => t.UserId == userId && t.ConsumedAt == null && t.SupersededAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (EmailToken token in open)
        {
            token.Supersede(now);
        }
    }

    /// <summary>Call after the commit: ends every session, so whoever held the account loses access.</summary>
    public async Task CompleteAsync(Guid userId, CancellationToken ct) =>
        await sessions.RevokeAllAsync(userId, ct);
}
