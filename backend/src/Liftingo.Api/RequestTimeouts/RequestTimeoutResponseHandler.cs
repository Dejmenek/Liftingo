using Microsoft.AspNetCore.Mvc;

namespace Liftingo.Api.RequestTimeouts;

internal static partial class RequestTimeoutResponseHandler
{
    public const string Code = "Request.Timeout";
    public const string DefaultPolicyName = "Default";

    public static RequestDelegate Create(string policyName) =>
        async httpContext =>
        {
            ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(RequestTimeoutResponseHandler).FullName!);

            LogTimedOut(logger, httpContext.Request.Method, httpContext.Request.Path, policyName, httpContext.TraceIdentifier);

            var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

            await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status504GatewayTimeout,
                    Title = "Gateway Timeout",
                    Detail = "The request took too long to complete. Try again later.",
                    Extensions = { ["code"] = Code },
                },
            });
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request timed out for {Method} {Path} on policy {Policy}. TraceId: {TraceId}")]
    private static partial void LogTimedOut(ILogger logger, string method, PathString path, string policy, string traceId);
}