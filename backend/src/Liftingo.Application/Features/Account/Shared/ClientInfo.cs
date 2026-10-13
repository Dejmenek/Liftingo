using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>What a slice knows about the caller: network address, device label and device id.</summary>
/// <param name="IpAddress">Personal data. Used only for audit events; never logged.</param>
/// <param name="DeviceType">The label from <see cref="DeviceDescriptor"/>.</param>
/// <param name="DeviceId">The client's device id, or a fresh id when it sent none.</param>
public sealed record ClientInfo(string? IpAddress, string DeviceType, Guid DeviceId)
{
    /// <param name="deviceIdHeader">The raw device id header. A missing or malformed one counts as a new device.</param>
    /// <param name="deviceIdOverride">
    /// A device id from somewhere other than the header (e.g. the start of an external sign-in). Wins over the
    /// header when given.
    /// </param>
    public static ClientInfo Create(string? ipAddress, string? userAgent, string? deviceIdHeader, Guid? deviceIdOverride = null)
    {
        Guid deviceId = deviceIdOverride is { } given && given != Guid.Empty
            ? given
            : ParseDeviceId(deviceIdHeader) ?? Guid.NewGuid();

        return new ClientInfo(ipAddress, DeviceDescriptor.Describe(userAgent), deviceId);
    }

    /// <returns>The device id, or <c>null</c> when <paramref name="value"/> is missing, malformed or empty.</returns>
    public static Guid? ParseDeviceId(string? value) =>
        Guid.TryParse(value, out Guid parsed) && parsed != Guid.Empty ? parsed : null;

    public SessionDevice ToSessionDevice() => new(DeviceId, DeviceType);
}
