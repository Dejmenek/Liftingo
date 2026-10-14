using Liftingo.Application.Common.Security;

namespace Liftingo.Api.Endpoints.Account.Shared;

/// <summary>
/// The body of every endpoint that signs a user in. It is the HTTP contract those
/// endpoints share, not an Application response. The refresh token never appears here: it goes in the cookie.
/// </summary>
public sealed record AuthSessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AccountResponse Account)
{
    public static AuthSessionResponse From(IssuedSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new AuthSessionResponse(session.AccessToken, session.AccessTokenExpiresAt, AccountResponse.From(session.Account));
    }
}
