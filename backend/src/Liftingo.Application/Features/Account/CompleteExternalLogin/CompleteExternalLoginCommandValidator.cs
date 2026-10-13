using FluentValidation;

using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.CompleteExternalLogin;

public sealed class CompleteExternalLoginCommandValidator : AbstractValidator<CompleteExternalLoginCommand>
{
    public CompleteExternalLoginCommandValidator()
    {
        RuleFor(c => c.AcceptTermsAndAge).MustAcceptTermsAndAge();
        RuleFor(c => c.AcceptHealthData).MustAcceptHealthData();

        // Whether the e-mail is required at all depends on the pending sign-in, so the handler checks that.
        When(c => !string.IsNullOrWhiteSpace(c.Email), () => RuleFor(c => c.Email!).MustBeValidEmail());
    }
}
