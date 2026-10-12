namespace Liftingo.Application.Common;

public interface IUserContext
{
    Guid UserId { get; }

    bool IsAuthenticated { get; }

    // From the "email_verified" claim of the access token.
    bool IsEmailVerified { get; }

    // From the "sid" claim; null when the token carries no valid session id.
    Guid? SessionId { get; }
}