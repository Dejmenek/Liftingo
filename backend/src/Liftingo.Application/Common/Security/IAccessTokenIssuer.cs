namespace Liftingo.Application.Common.Security;

public interface IAccessTokenIssuer
{
    AccessToken Issue(Guid userId, Guid sessionId, bool emailVerified);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);