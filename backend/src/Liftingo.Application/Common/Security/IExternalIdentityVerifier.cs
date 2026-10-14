namespace Liftingo.Application.Common.Security;

/// <summary>
/// Checks a credential the client got from an external provider (a Google ID token, a Facebook access token) with
/// that provider. Application never sees the provider's token format or HTTP calls.
/// </summary>
public interface IExternalIdentityVerifier
{
    /// <param name="loginProvider">The Identity login provider name, e.g. <c>Google</c>.</param>
    /// <param name="token">The credential as the client sent it.</param>
    /// <returns>
    /// The verified identity, or <c>null</c> when the credential is invalid, expired, issued to another application,
    /// or the provider could not be asked.
    /// </returns>
    Task<ExternalIdentity?> VerifyAsync(string loginProvider, string token, CancellationToken ct);
}