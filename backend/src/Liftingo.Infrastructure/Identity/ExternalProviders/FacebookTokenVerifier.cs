using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

using Liftingo.Application.Common.Security;

using Microsoft.Extensions.Logging;

namespace Liftingo.Infrastructure.Identity.ExternalProviders;

/// <summary>
/// Checks a Facebook access token with the Graph API. The <see cref="HttpClient"/> has the Graph API base address,
/// version included.
/// </summary>
internal sealed partial class FacebookTokenVerifier(
    HttpClient http,
    FacebookProviderOptions options,
    ILogger<FacebookTokenVerifier> logger) : IProviderTokenVerifier
{
    public string LoginProvider => FacebookProviderOptions.LoginProvider;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A token that cannot be verified for any reason is not verified; the failure is logged.")]
    public async Task<ExternalIdentity?> VerifyAsync(string token, CancellationToken ct)
    {
        try
        {
            return await VerifyCoreAsync(token, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // The exception is not logged: its message can quote the request URI, which carries the token.
            LogNotVerified(LoginProvider, ex.GetType().Name);

            return null;
        }
    }

    private async Task<ExternalIdentity?> VerifyCoreAsync(string token, CancellationToken ct)
    {
        // debug_token, asked with the app's own credentials, says which app the token was issued to. Without the
        // app id check a token issued to any other Facebook app would sign its owner in here.
        Uri debugUri = new(
            $"debug_token?input_token={Uri.EscapeDataString(token)}" +
            $"&access_token={Uri.EscapeDataString($"{options.AppId}|{options.AppSecret}")}",
            UriKind.Relative);

        DebugTokenResponse? debug = await GetAsync<DebugTokenResponse>(debugUri, "debug_token", ct);

        if (debug?.Data is not { IsValid: true, UserId: { Length: > 0 } userId } data
            || !string.Equals(data.AppId, options.AppId, StringComparison.Ordinal))
        {
            LogRejected(LoginProvider, "debug_token");

            return null;
        }

        // appsecret_proof proves the call comes from the app's server, not from someone holding just the token.
        Uri meUri = new(
            $"me?fields=id,name,email&access_token={Uri.EscapeDataString(token)}" +
            $"&appsecret_proof={AppSecretProof(token, options.AppSecret)}",
            UriKind.Relative);

        MeResponse? me = await GetAsync<MeResponse>(meUri, "me", ct);

        if (me is null || !string.Equals(me.Id, userId, StringComparison.Ordinal))
        {
            LogRejected(LoginProvider, "me");

            return null;
        }

        // Accounts registered with a phone number have no e-mail.
        string? email = me.Email?.Trim();

        return new ExternalIdentity(
            LoginProvider,
            me.Id,
            string.IsNullOrEmpty(email) ? null : email,
            me.Name);
    }

    internal static string AppSecretProof(string token, string appSecret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes(token)));

    private async Task<T?> GetAsync<T>(Uri uri, string call, CancellationToken ct)
        where T : class
    {
        using HttpResponseMessage response = await http.GetAsync(uri, ct);

        if (!response.IsSuccessStatusCode)
        {
            LogStatus(LoginProvider, call, (int)response.StatusCode);

            return null;
        }

        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A {Provider} token could not be verified ({ExceptionType}).")]
    private partial void LogNotVerified(string provider, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A {Provider} token was rejected at the {Call} check.")]
    private partial void LogRejected(string provider, string call);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The {Provider} {Call} call answered {StatusCode}.")]
    private partial void LogStatus(string provider, string call, int statusCode);

    private sealed record DebugTokenResponse([property: JsonPropertyName("data")] DebugTokenData? Data);

    private sealed record DebugTokenData(
        [property: JsonPropertyName("app_id")] string? AppId,
        [property: JsonPropertyName("is_valid")] bool IsValid,
        [property: JsonPropertyName("user_id")] string? UserId);

    private sealed record MeResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string? Email);
}
