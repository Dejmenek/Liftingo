using Liftingo.Api.Logging;

using Serilog;
using Serilog.Enrichers.Sensitive;
using Serilog.Events;

using System.Globalization;

namespace Liftingo.Api.Extensions;

internal static class SerilogExtensions
{
    public static Serilog.ILogger CreateBootstrapLogger() =>
        new LoggerConfiguration()
            .MaskSensitiveData()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .CreateBootstrapLogger();

    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .MaskSensitiveData());

        return builder;
    }

    // A safety net: code logs user ids only, and this masks any e-mail
    // or IP address that slips into a property or a message anyway. It is set up in code, not in
    // appsettings, so configuration cannot turn it off. Only the two operators we need run: the default
    // IBAN and credit-card ones risk matching ids and timestamps.
    internal static LoggerConfiguration MaskSensitiveData(this LoggerConfiguration loggerConfiguration) =>
        loggerConfiguration.Enrich.WithSensitiveDataMasking(options =>
        {
            options.Mode = MaskingMode.Globally;
            options.MaskingOperators =
            [
                new EmailAddressMaskingOperator(),
                new IpAddressMaskingOperator(),
            ];
        });

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