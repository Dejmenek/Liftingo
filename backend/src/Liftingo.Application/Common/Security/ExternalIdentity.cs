namespace Liftingo.Application.Common.Security;

/// <summary>
/// What an external provider vouched for after <see cref="IExternalIdentityVerifier"/> checked the client's
/// credential. Nothing about it is stored before the user gives consent.
/// </summary>
/// <param name="Provider">The Identity login provider name (<c>Google</c>, <c>Facebook</c>).</param>
/// <param name="ProviderKey">The user's id at the provider.</param>
/// <param name="Email">The provider's e-mail, treated as verified; <c>null</c> when the provider gave none.</param>
/// <param name="DisplayName">Shown in the user's list of linked logins.</param>
public sealed record ExternalIdentity(
    string Provider,
    string ProviderKey,
    string? Email,
    string? DisplayName);