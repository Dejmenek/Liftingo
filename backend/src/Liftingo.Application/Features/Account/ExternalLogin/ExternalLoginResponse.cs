using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.ExternalLogin;

/// <param name="Session">The new session, unless <paramref name="Outcome"/> is <see cref="ExternalLoginOutcome.LinkConfirmationSent"/>.</param>
public sealed record ExternalLoginResponse(ExternalLoginOutcome Outcome, IssuedSession? Session);
