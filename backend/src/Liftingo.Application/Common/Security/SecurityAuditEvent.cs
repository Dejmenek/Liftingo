using Liftingo.Domain.Account;

namespace Liftingo.Application.Common.Security;

/// <param name="UserId"><c>null</c> when the e-mail of a login attempt is unknown.</param>
/// <param name="Type">What happened.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="IpAddress">Personal data. <c>null</c> when the user id is <c>null</c> (FR-014).</param>
/// <param name="DeviceType">Personal data. <c>null</c> when the user id is <c>null</c> (FR-014).</param>
public sealed record SecurityAuditEvent(
    Guid? UserId,
    SecurityEventType Type,
    SecurityEventOutcome Outcome,
    DateTimeOffset OccurredAt,
    string? IpAddress,
    string? DeviceType);
