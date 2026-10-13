using Liftingo.Application.Common.Security;

using Microsoft.Extensions.Options;

namespace Liftingo.Api.Endpoints.Account.Shared;

/// <summary>
/// The <c>liftingo_rt</c> cookie. It is HttpOnly and only sent to the session endpoints, so page
/// scripts never see the refresh token.
/// </summary>
internal sealed class RefreshTokenCookie(IOptions<RefreshCookieOptions> options)
{
    public const string Name = "liftingo_rt";

    // The cookie never outlives the 30-day inactivity window, whatever the session has left.
    private static readonly TimeSpan s_maxLifetime = TimeSpan.FromDays(30);

    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies.TryGetValue(Name, out string? value) && !string.IsNullOrEmpty(value) ? value : null;
    }

    public void Append(HttpResponse response, IssuedSession session)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(session.RefreshToken);

        TimeSpan lifetime = session.RefreshTokenLifetime > s_maxLifetime ? s_maxLifetime : session.RefreshTokenLifetime;

        response.Cookies.Append(Name, session.RefreshToken, CreateOptions(lifetime));
    }

    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        // Path and attributes must match the cookie that was set, or the browser keeps the old one.
        response.Cookies.Delete(Name, CreateOptions(maxAge: null));
    }

    private CookieOptions CreateOptions(TimeSpan? maxAge) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = options.Value.Path,
            MaxAge = maxAge,
        };
}
