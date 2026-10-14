using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.RefreshSession;

/// <param name="Session">The renewed session with its rotated refresh token.</param>
public sealed record RefreshSessionResponse(IssuedSession Session);