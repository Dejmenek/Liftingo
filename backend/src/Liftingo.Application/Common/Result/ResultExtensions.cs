using Microsoft.AspNetCore.Http;

namespace Liftingo.Application.Common.Result;

public static class ResultExtensions
{
    public const string CodeExtension = "code";
    public const string ErrorsExtension = "errors";

    /// <summary>
    /// Maps a failed <see cref="Result"/> to an RFC 9457 ProblemDetails response.
    /// </summary>
    public static IResult ToProblem(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

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
        ErrorType.Failure => StatusCodes.Status400BadRequest,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped error type."),
    };
}