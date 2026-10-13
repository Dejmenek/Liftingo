using FluentValidation;

using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.ForgotPassword;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(c => c.Email).MustBeValidEmail();
}
