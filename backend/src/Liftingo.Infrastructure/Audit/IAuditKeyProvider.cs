namespace Liftingo.Infrastructure.Audit;

internal interface IAuditKeyProvider
{
    /// <summary>Gets the user's audit key, creating and saving it on the first call.</summary>
    Task<AuditKey> GetOrCreateAsync(Guid userId, CancellationToken ct);

    /// <summary>Finds a key by id, or <c>null</c> when it has been deleted with its account.</summary>
    Task<byte[]?> FindAsync(Guid keyId, CancellationToken ct);
}

/// <param name="Id">The <see cref="AuditEncryptionKey"/> id.</param>
/// <param name="Key">The 256-bit AES key.</param>
internal sealed record AuditKey(Guid Id, byte[] Key);
