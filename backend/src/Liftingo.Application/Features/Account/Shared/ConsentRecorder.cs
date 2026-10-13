using Liftingo.Application.Common.Persistence;
using Liftingo.Domain.Account;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>Stages consent rows. The caller saves.</summary>
public sealed class ConsentRecorder(
    IApplicationDbContext db,
    IOptions<LegalOptions> legal,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Adds exactly three rows, one per <see cref="ConsentType"/>. A declined marketing consent is
    /// stored as <c>false</c> so the refusal is provable.
    /// </summary>
    public void Stage(Guid userId, bool acceptTermsAndAge, bool acceptHealthData, bool acceptMarketing)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        string version = legal.Value.DocumentsVersion;

        db.UserConsents.Add(UserConsent.Record(userId, ConsentType.TermsAndAge, acceptTermsAndAge, version, now));
        db.UserConsents.Add(UserConsent.Record(userId, ConsentType.HealthData, acceptHealthData, version, now));
        db.UserConsents.Add(UserConsent.Record(userId, ConsentType.Marketing, acceptMarketing, version, now));
    }

    /// <summary>Marks every current consent row of the user as superseded; the history stays.</summary>
    public async Task SupersedeCurrentAsync(Guid userId, CancellationToken ct)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        List<UserConsent> current = await db.UserConsents
            .Where(c => c.UserId == userId && c.SupersededAt == null)
            .ToListAsync(ct);

        foreach (UserConsent consent in current)
        {
            consent.Supersede(now);
        }
    }
}
