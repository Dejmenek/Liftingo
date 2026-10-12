using System.Diagnostics.CodeAnalysis;

namespace Liftingo.Infrastructure.Audit;

/// <summary>
/// A per-user AES key that encrypts the IP address and device type in audit records. Deleting the
/// row (directly or through the account cascade) makes those fields unreadable.
/// </summary>
public class AuditEncryptionKey
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Mapped to a varbinary(max) column by EF Core.")]
    public byte[] ProtectedKey { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}