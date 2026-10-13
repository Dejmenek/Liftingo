namespace Liftingo.Application.Common.Security;

/// <summary>
/// What an external provider told us about the user. Api reads it from the short-lived external-login cookie and
/// passes it to the handlers; nothing about it is stored before the user gives consent (FR-021).
/// </summary>
/// <param name="Provider">The scheme name (<c>Google</c>, <c>Facebook</c>, <c>X</c>), which is also the Identity login provider.</param>
/// <param name="ProviderKey">The user's id at the provider.</param>
/// <param name="Email">The provider's e-mail, treated as verified; <c>null</c> when the provider gave none.</param>
/// <param name="DisplayName">Shown in the user's list of linked logins.</param>
/// <param name="DeviceId">The device id the client sent to the start endpoint, if any.</param>
public sealed record ExternalIdentity(
    string Provider,
    string ProviderKey,
    string? Email,
    string? DisplayName,
    Guid? DeviceId);
