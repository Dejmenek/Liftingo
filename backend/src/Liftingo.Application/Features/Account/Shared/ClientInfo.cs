using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>What a slice knows about the caller: network address, device label and device id.</summary>
/// <param name="IpAddress">Personal data. Used only for audit events; never logged.</param>
/// <param name="DeviceType">The label from <see cref="DeviceDescriptor"/>.</param>
/// <param name="DeviceId">The client's device id, or a fresh id when it sent none.</param>
public sealed record ClientInfo(string? IpAddress, string DeviceType, Guid DeviceId)
{
    /// <param name="deviceIdHeader">The raw device id header. A missing or malformed one counts as a new device.</param>
    public static ClientInfo Create(string? ipAddress, string? userAgent, string? deviceIdHeader) =>
        new(ipAddress, DeviceDescriptor.Describe(userAgent), ParseDeviceId(deviceIdHeader) ?? Guid.NewGuid());

    /// <returns>The device id, or <c>null</c> when <paramref name="value"/> is missing, malformed or empty.</returns>
    private static Guid? ParseDeviceId(string? value) =>
        Guid.TryParse(value, out Guid parsed) && parsed != Guid.Empty ? parsed : null;

    public SessionDevice ToSessionDevice() => new(DeviceId, DeviceType);
}
