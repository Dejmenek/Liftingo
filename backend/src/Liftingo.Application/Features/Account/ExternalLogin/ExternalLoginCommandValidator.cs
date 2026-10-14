using FluentValidation;

using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.ExternalLogin;

public sealed class ExternalLoginCommandValidator : AbstractValidator<ExternalLoginCommand>
{
    public ExternalLoginCommandValidator()
    {
        RuleFor(c => c.Token)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .WithErrorCode("Validation.TokenRequired")
            .WithMessage("Brakuje tokenu od dostawcy logowania.");

        // Missing consents are not field errors: whether they are needed at all depends on the account the
        // provider identity leads to, so the handler answers that with Auth.ConsentRequired. The same goes for
        // whether the e-mail is required.
        When(c => !string.IsNullOrWhiteSpace(c.Email), () => RuleFor(c => c.Email!).MustBeValidEmail());
    }
}
