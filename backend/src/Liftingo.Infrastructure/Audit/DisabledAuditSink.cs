using Liftingo.Domain.Account;

using Microsoft.Extensions.Logging;

namespace Liftingo.Infrastructure.Audit;

/// <summary>Used outside Development until the real sink exists: drops the record and warns.</summary>
internal sealed partial class DisabledAuditSink(ILogger<DisabledAuditSink> logger) : IAuditSink
{
    public Task WriteAsync(AuditRecord record, CancellationToken ct)
    {
        LogDropped(record.EventType);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The audit sink is disabled. Dropped a {EventType} audit event.")]
    private partial void LogDropped(SecurityEventType eventType);
}