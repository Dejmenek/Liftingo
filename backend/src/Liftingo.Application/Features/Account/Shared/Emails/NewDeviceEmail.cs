using Liftingo.Application.Common.Email;

namespace Liftingo.Application.Features.Account.Shared.Emails;

/// <summary>Security notice for a successful sign-in from a device the account has not used before.</summary>
/// <param name="SignedInAt">The sign-in time in UTC. The renderer shows it in the display time zone.</param>
/// <param name="DeviceType">The device label from <c>DeviceDescriptor</c>.</param>
public sealed record NewDeviceEmail(Guid UserId, string To, DateTimeOffset SignedInAt, string DeviceType)
    : OutgoingEmail(UserId, To);
