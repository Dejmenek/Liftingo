using Liftingo.Application.Common.Email;

namespace Liftingo.Application.Features.Account.Shared.Emails;

/// <summary>Asks the owner of an address to confirm linking a sign-in provider to the account.</summary>
/// <param name="Provider">The provider's scheme name, e.g. <c>Google</c>.</param>
public sealed record ProviderLinkConfirmationEmail(Guid UserId, string To, string Provider, string Token)
    : OutgoingEmail(UserId, To);