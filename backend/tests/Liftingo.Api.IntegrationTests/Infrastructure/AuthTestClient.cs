using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

using Liftingo.Api.Endpoints;
using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure.Fakers;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Infrastructure.Email;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

/// <summary>The <c>liftingo_rt</c> cookie as the API set it on a response.</summary>
/// <param name="Value">The refresh token; empty when the cookie was deleted.</param>
/// <param name="Attributes">Every attribute, e.g. <c>httponly</c>, <c>path</c>, <c>max-age</c> (names are matched case-insensitively).</param>
public sealed record RefreshCookie(string Value, IReadOnlyDictionary<string, string> Attributes)
{
    public bool HttpOnly => Attributes.ContainsKey("httponly");

    public bool Secure => Attributes.ContainsKey("secure");

    public string? SameSite => Attributes.GetValueOrDefault("samesite");

    public string? Path => Attributes.GetValueOrDefault("path");

    public long? MaxAgeSeconds => Attributes.TryGetValue("max-age", out string? value) ? long.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : null;

    // The API deletes a cookie by expiring it with an empty value.
    public bool IsDeleted => Value.Length == 0;
}

/// <summary>
/// One "device" calling the auth API: it owns a cookie-aware <see cref="HttpClient"/>, so the refresh
/// cookie from register/login/refresh is sent back automatically on the next call.
/// </summary>
public sealed partial class AuthTestClient(Func<bool, HttpClient> createClient) : IDisposable
{
    private const string ApiPrefix = "/api/v1";

    private readonly HttpClient _http = createClient(true);

    public HttpClient Http => _http;

    public static string Route(string authRoute) => ApiPrefix + authRoute;

    /// <summary>Registers an account. Without arguments it generates a unique e-mail and a valid password.</summary>
    public async Task<(HttpResponseMessage Response, RegisterRequestBody Request)> RegisterAsync(
        string? email = null,
        string? password = null,
        Guid? deviceId = null,
        string? clientIp = null)
    {
        RegisterRequestBody generated = new RegisterRequestFaker().Generate();
        RegisterRequestBody request = generated with
        {
            Email = email ?? generated.Email,
            Password = password ?? generated.Password,
        };

        return (await PostAsync(RouteConsts.Auth.Register, request, accessToken: null, deviceId, clientIp), request);
    }

    public Task<HttpResponseMessage> RegisterAsync(RegisterRequestBody request, Guid? deviceId = null) =>
        PostAsync(RouteConsts.Auth.Register, request, accessToken: null, deviceId);

    /// <param name="clientIp">Sent as the test host's <c>X-Test-Client-Ip</c> header, which sets the request's remote address.</param>
    /// <param name="userAgent">Sent as <c>User-Agent</c>, which the device label is derived from.</param>
    public Task<HttpResponseMessage> LoginAsync(
        string email,
        string password,
        Guid? deviceId = null,
        string? clientIp = null,
        string? userAgent = null) =>
        PostAsync(RouteConsts.Auth.Login, new { email, password }, accessToken: null, deviceId, clientIp, userAgent);

    /// <summary>Calls refresh with this client's own cookie.</summary>
    public Task<HttpResponseMessage> RefreshAsync(string? clientIp = null) =>
        PostAsync(RouteConsts.Auth.RefreshSession, body: null, accessToken: null, deviceId: null, clientIp);

    /// <summary>Calls refresh with an explicit cookie value (e.g. a replayed one), ignoring this client's cookies.</summary>
    public Task<HttpResponseMessage> RefreshWithCookieAsync(string refreshToken, string? clientIp = null) =>
        PostWithCookieAsync(RouteConsts.Auth.RefreshSession, refreshToken, clientIp);

    /// <summary>Calls logout with an explicit cookie value, ignoring this client's cookies.</summary>
    public Task<HttpResponseMessage> LogoutWithCookieAsync(string refreshToken) =>
        PostWithCookieAsync(RouteConsts.Auth.Logout, refreshToken);

    private async Task<HttpResponseMessage> PostWithCookieAsync(string authRoute, string refreshToken, string? clientIp = null)
    {
        using HttpClient cookieless = createClient(false);
        using var request = new HttpRequestMessage(HttpMethod.Post, Route(authRoute));
        request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={refreshToken}");

        if (clientIp is not null)
        {
            request.Headers.Add(TestEndpointsStartupFilter.ClientIpHeader, clientIp);
        }

        return await cookieless.SendAsync(request);
    }

    /// <summary>POSTs a JSON body (or none) to an auth route, optionally with a bearer token, using this client's cookies.</summary>
    public Task<HttpResponseMessage> PostJsonAsync(string authRoute, object? body, string? accessToken = null, string? clientIp = null) =>
        PostAsync(authRoute, body, accessToken, deviceId: null, clientIp);

    public Task<HttpResponseMessage> LogoutAsync(string? clientIp = null) =>
        PostAsync(RouteConsts.Auth.Logout, body: null, accessToken: null, deviceId: null, clientIp);

    public Task<HttpResponseMessage> LogoutAllAsync(string? accessToken, string? clientIp = null) =>
        PostAsync(RouteConsts.Auth.LogoutAllDevices, body: null, accessToken, deviceId: null, clientIp);

    public async Task<HttpResponseMessage> GetMeAsync(string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Route(RouteConsts.Auth.Me));

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await _http.SendAsync(request);
    }

    public static async Task<AuthSessionResponse> ReadSessionAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return await response.Content.ReadFromJsonAsync<AuthSessionResponse>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The response has no session body.");
    }

    /// <summary>Parses the <c>Set-Cookie</c> header for <c>liftingo_rt</c>, or returns <c>null</c> when there is none.</summary>
    public static RefreshCookie? ReadRefreshCookie(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
        {
            return null;
        }

        string prefix = RefreshTokenCookie.Name + "=";
        string? header = values.FirstOrDefault(v => v.StartsWith(prefix, StringComparison.Ordinal));

        if (header is null)
        {
            return null;
        }

        string[] parts = header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string part in parts.Skip(1))
        {
            int separator = part.IndexOf('=', StringComparison.Ordinal);

            if (separator < 0)
            {
                attributes[part] = string.Empty;
            }
            else
            {
                attributes[part[..separator]] = part[(separator + 1)..];
            }
        }

        return new RefreshCookie(parts[0][prefix.Length..], attributes);
    }

    /// <summary>Pulls the <c>token</c> query value out of the link in an e-mail's text body.</summary>
    public static string ExtractToken(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Match match = TokenInLink().Match(message.TextBody);

        return match.Success
            ? Uri.UnescapeDataString(match.Groups["token"].Value)
            : throw new InvalidOperationException($"No token link in the {message.TemplateName} e-mail.");
    }

    public void Dispose() => _http.Dispose();

    private async Task<HttpResponseMessage> PostAsync(
        string authRoute,
        object? body,
        string? accessToken,
        Guid? deviceId,
        string? clientIp = null,
        string? userAgent = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route(authRoute));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (deviceId is { } id)
        {
            request.Headers.Add(ClientInfoReader.DeviceIdHeader, id.ToString());
        }

        if (clientIp is not null)
        {
            request.Headers.Add(TestEndpointsStartupFilter.ClientIpHeader, clientIp);
        }

        if (userAgent is not null)
        {
            request.Headers.UserAgent.ParseAdd(userAgent);
        }

        return await _http.SendAsync(request);
    }

    [GeneratedRegex(@"[?&]token=(?<token>[^\s&""]+)")]
    private static partial Regex TokenInLink();
}
