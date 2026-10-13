namespace Liftingo.Application.Features.Account.HandleExternalLoginCallback;

public enum HandleExternalLoginCallbackOutcome
{
    // The user is signed in to an existing account.
    SignedIn,

    // A new user, or the owner of an unverified address: the consent step comes first. Nothing was
    // written, and the pending sign-in has to be kept for that step.
    ConsentRequired,
}
