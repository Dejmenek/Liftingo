namespace Liftingo.Application.Common.Security;

public sealed record SessionInfo(Guid SessionId, Guid UserId, SessionDevice Device);