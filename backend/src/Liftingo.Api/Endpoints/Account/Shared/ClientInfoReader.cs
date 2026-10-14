using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Api.Endpoints.Account.Shared;

internal static class ClientInfoReader
{
    public const string DeviceIdHeader = "X-Device-Id";

    public static ClientInfo Read(HttpContext http)
    {
        return ClientInfo.Create(
            http.Connection.RemoteIpAddress?.ToString(),
            http.Request.Headers.UserAgent.ToString(),
            http.Request.Headers[DeviceIdHeader].ToString());
    }
}