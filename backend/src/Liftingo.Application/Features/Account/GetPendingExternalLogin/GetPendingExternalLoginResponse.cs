namespace Liftingo.Application.Features.Account.GetPendingExternalLogin;

/// <param name="Provider">Route name of the provider, e.g. <c>google</c>.</param>
/// <param name="Email">The provider's e-mail, or <c>null</c> when it gave none.</param>
/// <param name="EmailRequired"><c>true</c> when the user has to type an e-mail (FR-023).</param>
/// <param name="AccountExists"><c>true</c> when the provider e-mail belongs to an unverified account, which the sign-in would take over (R10).</param>
public sealed record GetPendingExternalLoginResponse(string Provider, string? Email, bool EmailRequired, bool AccountExists);
