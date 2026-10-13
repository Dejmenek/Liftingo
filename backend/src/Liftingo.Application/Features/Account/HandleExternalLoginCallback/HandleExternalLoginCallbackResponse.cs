using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.HandleExternalLoginCallback;

/// <param name="Outcome">Where the browser goes next.</param>
/// <param name="Session">The new session when <paramref name="Outcome"/> is <see cref="HandleExternalLoginCallbackOutcome.SignedIn"/>.</param>
public sealed record HandleExternalLoginCallbackResponse(HandleExternalLoginCallbackOutcome Outcome, IssuedSession? Session);
