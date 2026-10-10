using Serilog;
using Serilog.Events;
using System.Globalization;

namespace Liftingo.Api.Extensions;

internal static class SerilogExtensions
{
    public static Serilog.ILogger CreateBootstrapLogger() =>
        new LoggerConfiguration()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .CreateBootstrapLogger();

    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        return builder;
    }

    public static WebApplication UseSerilogLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.IncludeQueryInRequestPath = false;

            options.GetLevel = static (httpContext, _, exception) => GetRequestLogLevel(httpContext, exception);
        });

        return app;
    }

    private static LogEventLevel GetRequestLogLevel(HttpContext httpContext, Exception? exception)
    {
        if (exception is not null)
        {
            return LogEventLevel.Verbose;
        }

        return httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : LogEventLevel.Information;
    }
}