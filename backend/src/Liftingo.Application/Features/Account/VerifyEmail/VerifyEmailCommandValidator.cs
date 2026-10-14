using FluentValidation;

namespace Liftingo.Application.Features.Account.VerifyEmail;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator() =>
        RuleFor(c => c.Token)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .WithErrorCode("Validation.TokenRequired")
            .WithMessage("Brakuje tokenu weryfikacyjnego.");
}