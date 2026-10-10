using Liftingo.Api.ExceptionHandling;

namespace Liftingo.Api.Extensions;

internal static class ExceptionHandlingExtensions
{
    public const string ExceptionDetailsExtension = "exception";

    public static IServiceCollection AddExceptionHandling(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            if (environment.IsDevelopment()
                && context.Exception is not null
                && context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
            {
                context.ProblemDetails.Extensions[ExceptionDetailsExtension] = new
                {
                    Type = context.Exception.GetType().FullName,
                    context.Exception.Message,
                    StackTrace = context.Exception.ToString(),
                };
            }
        });

        services.AddExceptionHandler<DomainExceptionHandler>();
        services.AddExceptionHandler<UnhandledExceptionLogger>();

        return services;
    }

    public static WebApplication UseExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            SuppressDiagnosticsCallback = _ => true,
        });

        app.UseStatusCodePages();

        return app;
    }
}