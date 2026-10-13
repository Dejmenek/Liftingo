using FluentValidation;

using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        // The other password rules (length, digit, special character) belong to Identity and are reported by
        // IIdentityService.CreateUserAsync with their own codes.
        RuleFor(c => c.Email).MustBeValidEmail();

        RuleFor(c => c.Password).MustBeAcceptablePassword();

        RuleFor(c => c.AcceptTermsAndAge).MustAcceptTermsAndAge();
        RuleFor(c => c.AcceptHealthData).MustAcceptHealthData();
    }
}
