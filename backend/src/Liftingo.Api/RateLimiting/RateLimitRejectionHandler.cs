using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Threading.RateLimiting;

namespace Liftingo.Api.RateLimiting;

internal static partial class RateLimitRejectionHandler
{
    public const string Code = "RateLimit.Exceeded";

    public static async ValueTask HandleAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        HttpContext httpContext = context.HttpContext;

        RateLimitPartitionInfo partition = RateLimitPartitionInfo.From(httpContext);

        ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(RateLimitRejectionHandler).FullName!);

        LogRejected(logger, partition.Type, partition.PolicyName, httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            int seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

            httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = "Too many requests. Try again later.",
                Extensions = { ["code"] = Code },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit exceeded for {PartitionType} partition on policy {Policy}. TraceId: {TraceId}")]
    private static partial void LogRejected(ILogger logger, RateLimitPartitionType partitionType, string policy, string traceId);
}