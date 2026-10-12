using System.Globalization;
using System.Security.Claims;

using Liftingo.Application.Common.Security;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>
/// What the provider told us, kept in the short-lived <c>Liftingo.External</c> cookie until the user finishes the
/// sign-in (R9). Nothing about it is stored in the database before then (FR-021).
/// </summary>
/// <param name="Provider">The scheme name (<c>Google</c>, <c>Facebook</c>, <c>X</c>), which is also the Identity login provider.</param>
/// <param name="ProviderKey">The user's id at the provider.</param>
/// <param name="Email">The provider's e-mail, treated as verified; <c>null</c> when the provider gave none.</param>
/// <param name="DisplayName">Shown in the user's list of linked logins.</param>
/// <param name="DeviceId">The device id the client sent to the start endpoint, if any.</param>
public sealed record PendingExternalLogin(
    string Provider,
    string ProviderKey,
    string? Email,
    string? DisplayName,
    Guid? DeviceId)
{
    /// <summary>The key of the scheme name in the properties that travel through the provider flow.</summary>
    public const string ProviderItem = "liftingo.provider";

    /// <summary>The key of the device id in the properties that travel through the provider flow.</summary>
    public const string DeviceIdItem = "liftingo.deviceId";

    /// <returns>The pending sign-in, or <c>null</c> when the cookie is missing, expired, tampered with or incomplete.</returns>
    public static async Task<PendingExternalLogin?> ReadAsync(HttpContext http)
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

        return new PendingExternalLogin(
            provider,
            providerKey,
            string.IsNullOrEmpty(email) ? null : email,
            result.Principal.FindFirstValue(ClaimTypes.Name),
            deviceId);
    }

    private static string? ItemOrNull(AuthenticationProperties properties, string key) =>
        properties.Items.TryGetValue(key, out string? value) ? value : null;

    /// <summary>Deletes the cookie, and with it everything the provider told us.</summary>
    public static Task ClearAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        return http.SignOutAsync(AuthenticationSchemes.External);
    }
}
