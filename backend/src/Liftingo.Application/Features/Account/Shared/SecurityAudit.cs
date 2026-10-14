using Liftingo.Application.Common.Security;
using Liftingo.Domain.Account;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>
/// Writes security audit events. IP address and device type are attached only when the event has a
/// user. A failure to write is logged and swallowed: auditing never fails the request.
/// </summary>
public sealed partial class SecurityAudit(
    ISecurityAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SecurityAudit> logger)
{
    /// <param name="userId"><c>null</c> for an attempt with an unknown e-mail: no IP and no device are recorded then.</param>
    /// <param name="client">
    /// The caller. To record a session's own device (logout, token reuse), pass
    /// <c>client with { DeviceType = session.Device.DeviceType }</c>.
    /// </param>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The audit write runs after the handler's commit and must never fail the request; the failure is logged.")]
    public async Task WriteAsync(
        Guid? userId,
        SecurityEventType type,
        SecurityEventOutcome outcome,
        ClientInfo client,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);

        LogEvent(type, outcome, userId);

        var auditEvent = new SecurityAuditEvent(
            userId,
            type,
            outcome,
            timeProvider.GetUtcNow(),
            userId is null ? null : client.IpAddress,
            userId is null ? null : client.DeviceType);

        try
        {
            await auditLog.WriteAsync(auditEvent, ct);
        }
        catch (Exception ex)
        {
            LogWriteFailed(ex, type, userId);
        }
    }

    // The plain log line holds event type, outcome and user id only: no IP, no device.
    [LoggerMessage(Level = LogLevel.Information, Message = "Security event {EventType} {Outcome} for user {UserId}.")]
    private partial void LogEvent(SecurityEventType eventType, SecurityEventOutcome outcome, Guid? userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Writing the {EventType} audit event failed for user {UserId}.")]
    private partial void LogWriteFailed(Exception exception, SecurityEventType eventType, Guid? userId);
}