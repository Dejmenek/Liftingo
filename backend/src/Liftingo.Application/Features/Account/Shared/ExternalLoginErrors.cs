using Liftingo.Application.Common.Result;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>Errors that more than one external-login slice answers with.</summary>
public static class ExternalLoginErrors
{
    // The provider credential is invalid, expired, or was issued to another application. The message names the
    // provider so the user knows which sign-in to retry, but never says which check failed.
    public static Error Failed(string provider) => Error.Unauthorized(
        "Auth.ExternalLoginFailed",
        $"Logowanie przez {provider} nie zostało dokończone. Zaloguj się ponownie.");

    // The provider identity is linked to one account, but the provider now reports another account's e-mail.
    public static readonly Error ProviderEmailConflict = Error.Conflict(
        "Auth.ProviderEmailConflict",
        "Adres e-mail od dostawcy należy do innego konta.");

    public static readonly Error ProviderUnavailable = Error.NotFound(
        "Auth.ProviderUnavailable",
        "Logowanie przez tego dostawcę jest niedostępne.");

    // Link confirmation for an account whose e-mail is not verified yet.
    public static readonly Error ConsentRequired = Error.Validation(
        "Auth.ConsentRequired",
        "Adres e-mail tego konta nie był jeszcze potwierdzony. Aby połączyć logowanie, potwierdź ponownie zgody: " +
        "ukończone 18 lat z akceptacją Regulaminu i Polityki prywatności oraz zgodę na przetwarzanie danych dotyczących zdrowia.");
}