namespace Liftingo.Application.Features.Account.CompleteExternalLogin;

public enum CompleteExternalLoginOutcome
{
    // A new account was created and signed in.
    AccountCreated,

    // The provider was linked to an existing account (a takeover of an unverified one) and the user signed in.
    SignedIn,

    // The typed-in address has an account: a link-confirmation e-mail went out instead (FR-023a).
    LinkConfirmationSent,
}
