using System.Net;
using System.Security.Claims;

namespace Liftingo.Api.RateLimiting;

internal enum RateLimitPartitionType
{
    User,
    Ip,
}

internal readonly record struct RateLimitPartitionInfo(RateLimitPartitionType Type, string Key)
{
    private const string UnknownIp = "unknown";

    public string PolicyName => Type == RateLimitPartitionType.User
        ? nameof(RateLimitingOptions.Authenticated)
        : nameof(RateLimitingOptions.Anonymous);

    public static RateLimitPartitionInfo From(HttpContext httpContext)
    {
        string? userId = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        if (!string.IsNullOrEmpty(userId))
        {
            return new RateLimitPartitionInfo(RateLimitPartitionType.User, $"user:{userId}");
        }

        IPAddress? ip = httpContext.Connection.RemoteIpAddress;

        if (ip is { IsIPv4MappedToIPv6: true })
        {
            ip = ip.MapToIPv4();
        }

        return new RateLimitPartitionInfo(RateLimitPartitionType.Ip, $"ip:{ip?.ToString() ?? UnknownIp}");
    }
}