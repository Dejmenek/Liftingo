using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.Register;

/// <param name="Session">The session the new account is signed in with (FR-006).</param>
public sealed record RegisterResponse(IssuedSession Session);
