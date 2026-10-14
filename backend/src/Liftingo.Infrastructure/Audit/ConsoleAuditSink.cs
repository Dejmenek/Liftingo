using Serilog;
using System.Globalization;

namespace Liftingo.Infrastructure.Audit;

/// <summary>
/// Development only. Writes the encrypted record to the console through its own Serilog logger,
/// not the application's pipeline, so the audit stream stays apart from application logs and cannot
/// reach sinks added to them later.
/// </summary>
internal sealed class ConsoleAuditSink : IAuditSink, IDisposable
{
    private const string OutputTemplate = "[AUDIT] {Message:lj}{NewLine}";
    private const string MessageTemplate =
        "{EventId} {UserId} {EventType} {Outcome} {OccurredAt} {KeyId} {IpAddressCipher} {DeviceTypeCipher}";

    private readonly Serilog.Core.Logger _logger = new LoggerConfiguration()
        .WriteTo.Console(outputTemplate: OutputTemplate, formatProvider: CultureInfo.InvariantCulture)
        .CreateLogger();

    public Task WriteAsync(AuditRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);

        _logger.Information(
            MessageTemplate,
            record.EventId,
            record.UserId,
            record.EventType,
            record.Outcome,
            record.OccurredAt,
            record.KeyId,
            record.IpAddressCipher,
            record.DeviceTypeCipher);

        return Task.CompletedTask;
    }

    public void Dispose() => _logger.Dispose();
}