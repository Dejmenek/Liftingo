using Liftingo.Application.Common.Security;

using Microsoft.AspNetCore.Http;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>What a slice knows about the caller: network address, device label and device id.</summary>
/// <param name="IpAddress">Personal data. Used only for audit events; never logged.</param>
/// <param name="DeviceType">The label from <see cref="DeviceDescriptor"/>.</param>
/// <param name="DeviceId">The client's <c>X-Device-Id</c>, or a fresh id when it sent none (R6).</param>
public sealed record ClientInfo(string? IpAddress, string DeviceType, Guid DeviceId)
{
    public const string DeviceIdHeader = "X-Device-Id";

    /// <param name="http">The current request.</param>
    /// <param name="deviceId">
    /// A device id from somewhere other than the header (e.g. a query parameter on the external
    /// challenge). Wins over the header when given.
    /// </param>
    public static ClientInfo From(HttpContext http, Guid? deviceId = null)
    {
        ArgumentNullException.ThrowIfNull(http);

        Guid resolved = deviceId is { } given && given != Guid.Empty ? given : FromHeader(http);

        return new ClientInfo(
            http.Connection.RemoteIpAddress?.ToString(),
            DeviceDescriptor.Describe(http.Request.Headers.UserAgent.ToString()),
            resolved);
    }

    public SessionDevice ToSessionDevice() => new(DeviceId, DeviceType);

    // A missing or malformed header counts as a new device: a fresh id.
    private static Guid FromHeader(HttpContext http)
    {
        bool valid = Guid.TryParse(http.Request.Headers[DeviceIdHeader].ToString(), out Guid parsed);

        return valid && parsed != Guid.Empty ? parsed : Guid.NewGuid();
    }
}
