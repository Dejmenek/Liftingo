using FluentValidation;

using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(c => c.Token)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .WithErrorCode("Validation.TokenRequired")
            .WithMessage("Brakuje tokenu resetu hasła.");

        RuleFor(c => c.NewPassword).MustBeAcceptablePassword();
    }
}