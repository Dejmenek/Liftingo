using FluentValidation;

namespace Liftingo.Application.Features.Account.ConfirmProviderLink;

public sealed class ConfirmProviderLinkCommandValidator : AbstractValidator<ConfirmProviderLinkCommand>
{
    public ConfirmProviderLinkCommandValidator()
    {
        RuleFor(c => c.Token)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .WithErrorCode("Validation.TokenRequired")
            .WithMessage("Brakuje tokenu potwierdzenia połączenia.");
    }
}