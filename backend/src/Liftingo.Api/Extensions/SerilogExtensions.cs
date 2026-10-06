using Serilog;
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
        // Query strings and request bodies can contain personal data, so they stay out of the logs.
        app.UseSerilogRequestLogging(options => options.IncludeQueryInRequestPath = false);

        return app;
    }
}