using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Globalization;

namespace Liftingo.Api.Http;

public static class ResultExtensions
{
    public const string CodeExtension = "code";
    public const string ErrorsExtension = "errors";
    public const string RetryAfterExtension = "retryAfter";
    public const string AccountExistsExtension = "accountExists";
    public const string EmailRequiredExtension = "emailRequired";

    /// <summary>
    /// Maps a failed <see cref="Result"/> to an RFC 9457 ProblemDetails response.
    /// </summary>
    public static IResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot map a successful result to a problem response.");
        }

        Error error = result.Error;

        Dictionary<string, object?> extensions = new() { [CodeExtension] = error.Code };

        if (error is ValidationError validationError)
        {
            extensions[ErrorsExtension] = validationError.Errors
                .Select(e => new ErrorDetail(e.Code, e.Description))
                .ToArray();

            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, extensions: extensions);
        }

        if (error is LockedOutError lockedOutError)
        {
            extensions[RetryAfterExtension] = lockedOutError.RetryAfter.ToString("O", CultureInfo.InvariantCulture);

            return new RetryAfterProblemResult(
                (ProblemHttpResult)Results.Problem(
                    detail: error.Description,
                    statusCode: ToStatusCode(error.Type),
                    extensions: extensions),
                lockedOutError.RetryAfter);
        }

        // The client needs these to word the consent form, so they travel as extensions next to the code.
        if (error is ConsentRequiredError consentRequiredError)
        {
            extensions[AccountExistsExtension] = consentRequiredError.AccountExists;
            extensions[EmailRequiredExtension] = consentRequiredError.EmailRequired;
        }

        return Results.Problem(
            detail: error.Description,
            statusCode: ToStatusCode(error.Type),
            extensions: extensions);
    }

    private static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        ErrorType.Failure => StatusCodes.Status400BadRequest,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped error type."),
    };
}