using Microsoft.AspNetCore.Diagnostics;

namespace Liftingo.Api.ExceptionHandling;

/// <summary>
/// Logs each unhandled exception once, at error level, with the trace id. Always returns
/// <c>false</c> so the <c>UseExceptionHandler()</c> fallback still writes the 500 response.
/// </summary>
internal sealed partial class UnhandledExceptionLogger(ILogger<UnhandledExceptionLogger> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!httpContext.RequestAborted.IsCancellationRequested)
        {
            LogUnhandledException(exception, httpContext.Request.Method, httpContext.Request.Path, httpContext.TraceIdentifier);
        }

        return ValueTask.FromResult(false);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}. TraceId: {TraceId}")]
    private partial void LogUnhandledException(Exception exception, string method, PathString path, string traceId);
}