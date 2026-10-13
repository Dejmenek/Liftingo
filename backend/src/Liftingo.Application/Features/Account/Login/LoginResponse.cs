using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.Login;

/// <param name="Session">The new session.</param>
public sealed record LoginResponse(IssuedSession Session);
