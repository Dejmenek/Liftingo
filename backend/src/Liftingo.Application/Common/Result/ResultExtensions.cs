using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace Liftingo.Application.Common.Result;

public static class ResultExtensions
{
    public const string CodeExtension = "code";
    public const string ErrorsExtension = "errors";
    public const string RetryAfterExtension = "retryAfter";

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

/// <summary>
/// A ProblemDetails response that also sends a <c>Retry-After</c> header in seconds. The seconds are
/// computed from the request's <see cref="TimeProvider"/> when the response is written.
/// </summary>
public sealed class RetryAfterProblemResult(ProblemHttpResult problem, DateTimeOffset retryAfter) : IResult, IStatusCodeHttpResult
{
    public ProblemHttpResult Problem { get; } = problem;

    public DateTimeOffset RetryAfter { get; } = retryAfter;

    public int? StatusCode => Problem.StatusCode;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        TimeProvider timeProvider = httpContext.RequestServices?.GetService(typeof(TimeProvider)) as TimeProvider
            ?? TimeProvider.System;

        double seconds = Math.Ceiling((RetryAfter - timeProvider.GetUtcNow()).TotalSeconds);
        long retryAfterSeconds = (long)Math.Max(0, seconds);

        httpContext.Response.Headers[HeaderNames.RetryAfter] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        return Problem.ExecuteAsync(httpContext);
    }
}