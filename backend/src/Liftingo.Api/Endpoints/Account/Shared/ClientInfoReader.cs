using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Api.Endpoints.Account.Shared;

/// <summary>Reads what a use case needs to know about the caller from the request.</summary>
internal static class ClientInfoReader
{
    public const string DeviceIdHeader = "X-Device-Id";

    /// <param name="deviceIdOverride">A device id from somewhere other than the header; wins over it when given.</param>
    public static ClientInfo Read(HttpContext http, Guid? deviceIdOverride = null)
    {
        ArgumentNullException.ThrowIfNull(http);

        return ClientInfo.Create(
            http.Connection.RemoteIpAddress?.ToString(),
            http.Request.Headers.UserAgent.ToString(),
            http.Request.Headers[DeviceIdHeader].ToString(),
            deviceIdOverride);
    }

    /// <summary>The header's device id when it has a valid one, otherwise <paramref name="fallbackDeviceId"/>.</summary>
    public static ClientInfo ReadPreferringHeader(HttpContext http, Guid? fallbackDeviceId)
    {
        ArgumentNullException.ThrowIfNull(http);

        bool headerHasId = ClientInfo.ParseDeviceId(http.Request.Headers[DeviceIdHeader].ToString()) is not null;

        return Read(http, headerHasId ? null : fallbackDeviceId);
    }
}
