using Liftingo.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.Globalization;

namespace Liftingo.Api.ExceptionHandling;

/// <summary>
/// Maps <see cref="SessionStoreUnavailableException"/> (Redis is down) to a 503 ProblemDetails response
/// with <c>Retry-After</c>.
/// </summary>
internal sealed partial class SessionStoreUnavailableExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<SessionStoreUnavailableExceptionHandler> logger) : IExceptionHandler
{
    public const string ErrorCode = "Service.Unavailable";
    public const int RetryAfterSeconds = 30;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not SessionStoreUnavailableException)
        {
            return false;
        }

        LogSessionStoreUnavailable(httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers[HeaderNames.RetryAfter] = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "Usługa jest chwilowo niedostępna. Spróbuj ponownie za chwilę.",
                Extensions = { ["code"] = ErrorCode },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The session store is unavailable. TraceId: {TraceId}")]
    private partial void LogSessionStoreUnavailable(string traceId);
}