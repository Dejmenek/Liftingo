using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.ConfirmProviderLink;

/// <param name="Session">The session the user is signed in with once the provider is linked.</param>
public sealed record ConfirmProviderLinkResponse(IssuedSession Session);