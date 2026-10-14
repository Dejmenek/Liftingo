namespace Liftingo.Application.Features.Account.ExternalLogin;

public enum ExternalLoginOutcome
{
    // A new account was created and signed in.
    AccountCreated,

    // An existing account was signed in, with the provider linked to it if it was not already.
    SignedIn,

    // The typed-in address has an account: a link-confirmation e-mail went out instead of a sign-in.
    LinkConfirmationSent,
}