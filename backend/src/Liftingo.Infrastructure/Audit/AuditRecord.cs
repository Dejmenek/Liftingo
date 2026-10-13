using Liftingo.Domain.Account;

namespace Liftingo.Infrastructure.Audit;

/// <summary>
/// What leaves the application for the audit stream. IP address and device type are encrypted with the
/// user's key; an event without a user has an empty <see cref="KeyId"/> and empty ciphers.
/// </summary>
/// <param name="UserId">Plain text, so a query can filter by user.</param>
/// <param name="KeyId">The <see cref="AuditEncryptionKey"/> used, or <see cref="Guid.Empty"/>.</param>
/// <param name="IpAddressCipher">Base64 of nonce + ciphertext + tag, or empty.</param>
/// <param name="DeviceTypeCipher">Base64 of nonce + ciphertext + tag, or empty.</param>
public sealed record AuditRecord(
    Guid EventId,
    Guid? UserId,
    SecurityEventType EventType,
    SecurityEventOutcome Outcome,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    string IpAddressCipher,
    string DeviceTypeCipher);
