using FluentValidation.Results;

namespace Liftingo.Application.Common.Result;

public static class ValidationResultExtensions
{
    /// <summary>
    /// Turns the failures of a FluentValidation run into a <see cref="ValidationError"/>. Every rule needs an
    /// error code (<c>WithErrorCode</c>), because the code is the contract the client reads.
    /// </summary>
    public static ValidationError ToValidationError(this ValidationResult validationResult)
    {
        return ValidationError.FromErrors(
        [
            .. validationResult.Errors.Select(failure => Error.Validation(failure.ErrorCode, failure.ErrorMessage)),
        ]);
    }
}