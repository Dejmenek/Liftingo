using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Api.Endpoints.Account.Shared;

internal static class ClientInfoReader
{
    public const string DeviceIdHeader = "X-Device-Id";

    public static ClientInfo Read(HttpContext http, Guid? deviceIdOverride = null)
    {
        return ClientInfo.Create(
            http.Connection.RemoteIpAddress?.ToString(),
            http.Request.Headers.UserAgent.ToString(),
            http.Request.Headers[DeviceIdHeader].ToString(),
            deviceIdOverride);
    }

    public static ClientInfo ReadPreferringHeader(HttpContext http, Guid? fallbackDeviceId)
    {
        bool headerHasId = ClientInfo.ParseDeviceId(http.Request.Headers[DeviceIdHeader].ToString()) is not null;

        return Read(http, headerHasId ? null : fallbackDeviceId);
    }
}
