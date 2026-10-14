using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.Login;

public sealed record LoginResponse(IssuedSession Session);