using Liftingo.Application.Common.Result;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>Errors that more than one external-login slice answers with.</summary>
public static class ExternalLoginErrors
{
    public static readonly Error Expired = Error.Validation(
        "Auth.ExternalLoginExpired",
        "Logowanie przez dostawcę wygasło albo zostało anulowane. Zaloguj się ponownie.");

    public static readonly Error ConsentRequired = Error.Validation(
        "Auth.ConsentRequired",
        "Adres e-mail tego konta nie był jeszcze potwierdzony. Aby połączyć logowanie, potwierdź ponownie zgody: " +
        "ukończone 18 lat z akceptacją Regulaminu i Polityki prywatności oraz zgodę na przetwarzanie danych dotyczących zdrowia.");
}
