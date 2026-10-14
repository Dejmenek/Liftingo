namespace Liftingo.Application.Common.Security;

public interface ISecurityAuditLog
{
    Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken ct);
}