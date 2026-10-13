using System.ComponentModel.DataAnnotations;

using FluentValidation;

namespace Liftingo.Application.Features.Account.Shared;

/// <summary>
/// FluentValidation rules shared by the slices that take an e-mail or the two required consents.
/// Error codes are the contract's <c>Validation.*</c> codes; messages are Polish.
/// </summary>
public static class AccountValidationRules
{
    public const int EmailMaxLength = 256;
    public const int PasswordMaxLength = 128;

    // The same check FluentValidation's EmailAddress() does in its ASP.NET-compatible mode.
    private static readonly EmailAddressAttribute s_emailFormat = new();

    /// <summary>
    /// Validates the trimmed e-mail: present, at most 256 characters, valid format. It trims by
    /// itself, so the slice must still use the trimmed value when it stores or looks up the address.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(this IRuleBuilder<T, string> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .Must(email => !string.IsNullOrWhiteSpace(email))
            .WithErrorCode("Validation.EmailRequired")
            .WithMessage("Podaj adres e-mail.")
            .Must(email => email is null || email.Trim().Length <= EmailMaxLength)
            .WithErrorCode("Validation.EmailTooLong")
            .WithMessage($"Adres e-mail może mieć najwyżej {EmailMaxLength} znaków.")
            .Must(email => string.IsNullOrWhiteSpace(email) || s_emailFormat.IsValid(email.Trim()))
            .WithErrorCode("Validation.EmailInvalid")
            .WithMessage("Podaj poprawny adres e-mail.");
    }

    /// <summary>
    /// A new password is present and at most 128 characters. The other policy rules (length, digit, special
    /// character) belong to Identity and are reported by <c>IIdentityService</c> with their own codes.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeAcceptablePassword<T>(this IRuleBuilder<T, string> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .Must(password => !string.IsNullOrEmpty(password))
            .WithErrorCode("Validation.PasswordRequired")
            .WithMessage("Podaj hasło.")
            .Must(password => password is null || password.Length <= PasswordMaxLength)
            .WithErrorCode("Validation.PasswordTooLong")
            .WithMessage($"Hasło może mieć najwyżej {PasswordMaxLength} znaków.");
    }

    public static IRuleBuilderOptions<T, bool> MustAcceptTermsAndAge<T>(this IRuleBuilder<T, bool> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .Must(accepted => accepted)
            .WithErrorCode("Validation.TermsRequired")
            .WithMessage("Aby założyć konto, potwierdź, że masz ukończone 18 lat oraz akceptujesz Regulamin i Politykę prywatności.");
    }

    public static IRuleBuilderOptions<T, bool> MustAcceptHealthData<T>(this IRuleBuilder<T, bool> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .Must(accepted => accepted)
            .WithErrorCode("Validation.HealthDataRequired")
            .WithMessage(
                "Zgoda na przetwarzanie danych dotyczących zdrowia jest wymagana. " +
                "Potrzebujemy Twojej wagi ciała, aby bezpiecznie dobrać obciążenia startowe.");
    }
}
