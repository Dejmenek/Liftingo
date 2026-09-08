namespace Liftingo.Application.Common.Result;

public sealed record ValidationError : Error
{
    public IReadOnlyList<Error> Errors { get; }

    public ValidationError(IReadOnlyList<Error> errors)
        : base("Validation.General", "One or more validation errors occurred.", ErrorType.Validation)
    {
        Errors = errors;
    }

    public static ValidationError FromErrors(IReadOnlyList<Error> errors) => new(errors);
}