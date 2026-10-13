using System.Globalization;
using System.Security.Claims;

using Liftingo.Api.Security;
using Liftingo.Application.Common.Security;

using Microsoft.AspNetCore.Authentication;

namespace Liftingo.Api.Endpoints.Account.Shared;

/// <summary>
/// The short-lived <c>Liftingo.External</c> cookie that holds what the provider told us until the user finishes the
/// sign-in. Nothing about it is stored in the database before then.
/// </summary>
internal static class ExternalLoginCookie
{
    /// <summary>The key of the scheme name in the properties that travel through the provider flow.</summary>
    public const string ProviderItem = "liftingo.provider";

    /// <summary>The key of the device id in the properties that travel through the provider flow.</summary>
    public const string DeviceIdItem = "liftingo.deviceId";

    /// <returns>The pending sign-in, or <c>null</c> when the cookie is missing, expired, tampered with or incomplete.</returns>
    public static async Task<ExternalIdentity?> ReadAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        AuthenticateResult result = await http.AuthenticateAsync(AuthenticationSchemes.External);

        if (!result.Succeeded || result.Principal is null || result.Properties is null)
        {
            return null;
        }

        string? provider = ItemOrNull(result.Properties, ProviderItem);
        string? providerKey = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerKey))
        {
            return null;
        }

        string? email = result.Principal.FindFirstValue(ClaimTypes.Email)?.Trim();
        Guid? deviceId = Guid.TryParse(ItemOrNull(result.Properties, DeviceIdItem), CultureInfo.InvariantCulture, out Guid parsed)
            && parsed != Guid.Empty
                ? parsed
                : null;

        return new ExternalIdentity(
            provider,
            providerKey,
            string.IsNullOrEmpty(email) ? null : email,
            result.Principal.FindFirstValue(ClaimTypes.Name),
            deviceId);
    }

    /// <summary>Deletes the cookie, and with it everything the provider told us.</summary>
    public static Task ClearAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        return http.SignOutAsync(AuthenticationSchemes.External);
    }

    private static string? ItemOrNull(AuthenticationProperties properties, string key) =>
        properties.Items.TryGetValue(key, out string? value) ? value : null;
}
