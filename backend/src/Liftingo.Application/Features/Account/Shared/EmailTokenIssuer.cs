using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Security;
using Liftingo.Domain.Account;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>The provider identity a <see cref="EmailTokenPurpose.ProviderLinkConfirmation"/> token carries.</summary>
public sealed record ProviderLinkInfo(string LoginProvider, string ProviderKey);

/// <summary>
/// Issues single-use e-mail tokens. It only stages changes: the caller saves, then sends the e-mail.
/// </summary>
public sealed class EmailTokenIssuer(
    IApplicationDbContext db,
    IOptions<EmailTokensOptions> options,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan s_limitWindow = TimeSpan.FromHours(1);

    public Task<string?> IssueAsync(Guid userId, EmailTokenPurpose purpose, CancellationToken ct) =>
        IssueAsync(userId, purpose, provider: null, ct);

    /// <returns>
    /// The token to put in the e-mail, or <c>null</c> when the user already has
    /// <see cref="EmailTokensOptions.MaxPerAddressPerHour"/> tokens of this purpose from the last hour:
    /// the caller answers normally and sends nothing.
    /// </returns>
    public async Task<string?> IssueAsync(
        Guid userId,
        EmailTokenPurpose purpose,
        ProviderLinkInfo? provider,
        CancellationToken ct)
    {
        EmailTokensOptions tokens = options.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset windowStart = now - s_limitWindow;

        int recent = await db.EmailTokens
            .CountAsync(t => t.UserId == userId && t.Purpose == purpose && t.CreatedAt > windowStart, ct);

        if (recent >= tokens.MaxPerAddressPerHour)
        {
            return null;
        }

        List<EmailToken> open = await db.EmailTokens
            .Where(t => t.UserId == userId
                && t.Purpose == purpose
                && t.ConsumedAt == null
                && t.SupersededAt == null
                && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (EmailToken token in open)
        {
            token.Supersede(now);
        }

        (string value, byte[] hash) = OpaqueToken.Generate();
        TimeSpan lifetime = LifetimeOf(purpose, tokens);

        db.EmailTokens.Add(provider is null
            ? EmailToken.Issue(userId, purpose, hash, now, lifetime)
            : EmailToken.IssueProviderLink(userId, hash, provider.LoginProvider, provider.ProviderKey, now, lifetime));

        return value;
    }

    /// <summary>Finds the token whatever its state, so the caller can tell "already used" from "unknown". Tracked.</summary>
    public async Task<EmailToken?> FindAsync(string token, EmailTokenPurpose purpose, CancellationToken ct)
    {
        byte[]? hash = OpaqueToken.Hash(token);

        if (hash is null)
        {
            return null;
        }

        return await db.EmailTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);
    }

    private static TimeSpan LifetimeOf(EmailTokenPurpose purpose, EmailTokensOptions tokens) =>
        purpose switch
        {
            EmailTokenPurpose.EmailVerification => tokens.VerificationLifetime,
            EmailTokenPurpose.PasswordReset => tokens.PasswordResetLifetime,
            EmailTokenPurpose.ProviderLinkConfirmation => tokens.ProviderLinkLifetime,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown e-mail token purpose."),
        };
}