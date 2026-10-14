namespace Liftingo.Infrastructure.Audit;

/// <summary>Where encrypted audit records go. The production sink (Azure Monitor Log Analytics) is built later.</summary>
internal interface IAuditSink
{
    Task WriteAsync(AuditRecord record, CancellationToken ct);
}