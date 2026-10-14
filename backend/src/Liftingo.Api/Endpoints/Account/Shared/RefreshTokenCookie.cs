using Liftingo.Application.Common.Security;

using Microsoft.Extensions.Options;

namespace Liftingo.Api.Endpoints.Account.Shared;

internal sealed class RefreshTokenCookie(IOptions<RefreshCookieOptions> options)
{
    public const string Name = "liftingo_rt";

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
