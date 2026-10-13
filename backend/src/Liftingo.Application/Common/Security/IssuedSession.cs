namespace Liftingo.Application.Common.Security;

/// <summary>
/// A session that a use case has just started or renewed. Api puts the access token in the response body and the
/// refresh token in the HttpOnly cookie (R22); Application never touches either transport.
/// </summary>
/// <param name="AccessToken">The short-lived JWT.</param>
/// <param name="AccessTokenExpiresAt">When the JWT expires.</param>
/// <param name="Account">The signed-in account as it is now.</param>
/// <param name="RefreshToken">The opaque refresh token. Never logged, never in a response body.</param>
/// <param name="RefreshTokenLifetime">How long the session has left, so the cookie expires with it.</param>
public sealed record IssuedSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    AccountSummary Account,
    string RefreshToken,
    TimeSpan RefreshTokenLifetime);
