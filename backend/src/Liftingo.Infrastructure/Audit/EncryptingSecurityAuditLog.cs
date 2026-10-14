using Liftingo.Application.Common.Security;

namespace Liftingo.Infrastructure.Audit;

/// <summary>
/// Encrypts the personal fields of an audit event (IP address, device type) under the user's key and
/// hands the record to the <see cref="IAuditSink"/>. Events without a user hold no personal data and
/// are passed on with empty ciphers.
/// </summary>
internal sealed class EncryptingSecurityAuditLog(IAuditKeyProvider keys, IAuditSink sink) : ISecurityAuditLog
{
    public async Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        AuditRecord record;

        if (auditEvent.UserId is { } userId)
        {
            AuditKey key = await keys.GetOrCreateAsync(userId, ct);

            record = new AuditRecord(
                Guid.NewGuid(),
                userId,
                auditEvent.Type,
                auditEvent.Outcome,
                auditEvent.OccurredAt,
                key.Id,
                AuditCipher.Encrypt(key.Key, auditEvent.IpAddress),
                AuditCipher.Encrypt(key.Key, auditEvent.DeviceType));
        }
        else
        {
            record = new AuditRecord(
                Guid.NewGuid(),
                null,
                auditEvent.Type,
                auditEvent.Outcome,
                auditEvent.OccurredAt,
                Guid.Empty,
                string.Empty,
                string.Empty);
        }

        await sink.WriteAsync(record, ct);
    }
}