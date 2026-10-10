using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

/// <param name="connectionString">The test container's connection string.</param>
/// <param name="rateLimitingSettings">
/// <c>RateLimiting</c> configuration overrides. When <c>null</c>, rate limiting is switched off so
/// only the tests that opt in are affected by it.
/// </param>
public sealed class ApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? rateLimitingSettings = null) : WebApplicationFactory<Program>
{
    public const string EnvironmentName = "Testing";

    public LogEventCollector Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        builder.ConfigureServices(services =>
        {
            services.AddTestEndpoints(Logs);

            if (rateLimitingSettings is null)
            {
                services.PostConfigure<RateLimiterOptions>(options => options.GlobalLimiter = null);
            }
        });

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
            });

            if (rateLimitingSettings is not null)
            {
                configuration.AddInMemoryCollection(rateLimitingSettings);
            }
        });
    }
}