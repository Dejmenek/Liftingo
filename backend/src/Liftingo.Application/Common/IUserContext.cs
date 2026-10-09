namespace Liftingo.Application.Common;

public interface IUserContext
{
    Guid UserId { get; }

    bool IsAuthenticated { get; }
}
