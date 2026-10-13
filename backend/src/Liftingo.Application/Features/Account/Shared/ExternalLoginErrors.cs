using Liftingo.Application.Common.Result;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>Errors that more than one external-login slice answers with.</summary>
public static class ExternalLoginErrors
{
    // The provider failed, the user cancelled there, or the external cookie is missing or invalid (FR-020). The
    // callback never shows this as a body: Api redirects to the login page.
    public static readonly Error Failed = Error.Failure(
        "Auth.ExternalLoginFailed",
        "Logowanie przez dostawcę nie zostało dokończone.");

    // The provider identity is linked to one account, but the provider now reports another account's e-mail
    // (FR-022a). Api redirects to the login page.
    public static readonly Error ProviderEmailConflict = Error.Conflict(
        "Auth.ProviderEmailConflict",
        "Adres e-mail od dostawcy należy do innego konta.");

    public static readonly Error Expired = Error.Validation(
        "Auth.ExternalLoginExpired",
        "Logowanie przez dostawcę wygasło albo zostało anulowane. Zaloguj się ponownie.");

    public static readonly Error ConsentRequired = Error.Validation(
        "Auth.ConsentRequired",
        "Adres e-mail tego konta nie był jeszcze potwierdzony. Aby połączyć logowanie, potwierdź ponownie zgody: " +
        "ukończone 18 lat z akceptacją Regulaminu i Polityki prywatności oraz zgodę na przetwarzanie danych dotyczących zdrowia.");
}
