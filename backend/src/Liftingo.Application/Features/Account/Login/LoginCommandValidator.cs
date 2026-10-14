using FluentValidation;

namespace Liftingo.Application.Features.Account.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Only presence is checked: a malformed address gets the same 401 as any other wrong credential.
        RuleFor(c => c.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email))
            .WithErrorCode("Validation.EmailRequired")
            .WithMessage("Podaj adres e-mail.");

        RuleFor(c => c.Password)
            .Must(password => !string.IsNullOrEmpty(password))
            .WithErrorCode("Validation.PasswordRequired")
            .WithMessage("Podaj hasło.");
    }
}