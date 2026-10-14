using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using System.Globalization;

namespace Liftingo.Api.Http;

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