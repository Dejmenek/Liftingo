using System.Security.Cryptography;

using Liftingo.Infrastructure.Persistence;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Liftingo.Infrastructure.Audit;

/// <summary>
/// Gets or creates a user's 256-bit audit key, kept wrapped with Data Protection. It saves on its own
/// because audit events are written after the handler's commit.
/// </summary>
internal sealed class AuditKeyProvider(
    ApplicationDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider) : IAuditKeyProvider
{
    public const string Purpose = "Liftingo.AuditKeys";

    private const int KeyBytes = 32;

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    public async Task<AuditKey> GetOrCreateAsync(Guid userId, CancellationToken ct)
    {
        AuditEncryptionKey? existing = await db.AuditEncryptionKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.UserId == userId, ct);

        if (existing is not null)
        {
            return Unwrap(existing);
        }

        var created = new AuditEncryptionKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProtectedKey = _protector.Protect(RandomNumberGenerator.GetBytes(KeyBytes)),
            CreatedAt = timeProvider.GetUtcNow(),
        };

        db.AuditEncryptionKeys.Add(created);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two events for a new user raced: the unique index on UserId let the other one win.
            db.Entry(created).State = EntityState.Detached;

            AuditEncryptionKey winner = await db.AuditEncryptionKeys.AsNoTracking()
                .FirstAsync(k => k.UserId == userId, ct);

            return Unwrap(winner);
        }

        return Unwrap(created);
    }

    public async Task<byte[]?> FindAsync(Guid keyId, CancellationToken ct)
    {
        AuditEncryptionKey? key = await db.AuditEncryptionKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == keyId, ct);

        return key is null ? null : _protector.Unprotect(key.ProtectedKey);
    }

    private AuditKey Unwrap(AuditEncryptionKey key) => new(key.Id, _protector.Unprotect(key.ProtectedKey));
}
