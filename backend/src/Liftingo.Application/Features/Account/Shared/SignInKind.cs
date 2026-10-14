namespace Liftingo.Application.Features.Account.Shared;

public enum SignInKind
{
    // The account was just created: its first device is stored and is never "new".
    Registration,

    // A sign-in to an existing account: an unknown device is new.
    SignIn,
}