using Liftingo.Application.Common.Result;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>
/// A provider sign-in that would create an account or take over an unverified one came without both required
/// consents. Nothing was written; the client shows the consent form and sends the same provider token again.
/// </summary>
public sealed record ConsentRequiredError : Error
{
    public const string ErrorCode = "Auth.ConsentRequired";

    /// <summary>
    /// <c>true</c> when an unverified account with the provider's e-mail exists and would be taken over, so the page
    /// can explain why consents are asked again.
    /// </summary>
    public bool AccountExists { get; }

    /// <summary><c>true</c> when the provider gave no e-mail and the request carried none.</summary>
    public bool EmailRequired { get; }

    public ConsentRequiredError(bool accountExists, bool emailRequired)
        : base(
            ErrorCode,
            "Aby dokończyć logowanie przez dostawcę, potwierdź, że masz ukończone 18 lat i akceptujesz Regulamin oraz " +
            "Politykę prywatności, i wyraź zgodę na przetwarzanie danych dotyczących zdrowia.",
            ErrorType.Validation)
    {
        AccountExists = accountExists;
        EmailRequired = emailRequired;
    }
}