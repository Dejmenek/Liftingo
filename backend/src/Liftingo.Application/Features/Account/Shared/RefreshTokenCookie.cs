using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>
/// The <c>liftingo_rt</c> cookie. It is HttpOnly and only sent to the session endpoints, so page
/// scripts never see the refresh token (FR-008a).
/// </summary>
public sealed class RefreshTokenCookie(IOptions<SessionsOptions> options)
{
    public const string Name = "liftingo_rt";

    // The cookie never outlives the 30-day inactivity window, whatever the session has left.
    private static readonly TimeSpan s_maxLifetime = TimeSpan.FromDays(30);

    public void Append(HttpResponse response, string token, TimeSpan maxAge)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrEmpty(token);

        TimeSpan lifetime = maxAge > s_maxLifetime ? s_maxLifetime : maxAge;

        response.Cookies.Append(Name, token, CreateOptions(lifetime));
    }

    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        // Path and attributes must match the cookie that was set, or the browser keeps the old one.
        response.Cookies.Delete(Name, CreateOptions(maxAge: null));
    }

    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies.TryGetValue(Name, out string? value) && !string.IsNullOrEmpty(value) ? value : null;
    }

    private CookieOptions CreateOptions(TimeSpan? maxAge) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = options.Value.CookiePath,
            MaxAge = maxAge,
        };
}
