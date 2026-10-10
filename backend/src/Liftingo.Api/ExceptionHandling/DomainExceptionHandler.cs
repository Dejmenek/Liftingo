using Liftingo.Domain.Common;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Liftingo.Api.ExceptionHandling;

/// <summary>
/// Maps <see cref="DomainException"/> to a 422 ProblemDetails response. Other exceptions are
/// left to the <c>UseExceptionHandler()</c> fallback, which returns the generic 500.
/// </summary>
internal sealed partial class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException)
        {
            return false;
        }

        LogDomainException(exception.GetType().Name, httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = exception.Message,
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Domain rule violated ({ExceptionType}). TraceId: {TraceId}")]
    private partial void LogDomainException(string exceptionType, string traceId);
}