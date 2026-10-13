using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.CompleteExternalLogin;

/// <param name="Session">The new session, unless <paramref name="Outcome"/> is <see cref="CompleteExternalLoginOutcome.LinkConfirmationSent"/>.</param>
public sealed record CompleteExternalLoginResponse(CompleteExternalLoginOutcome Outcome, IssuedSession? Session);
