using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

/// <param name="connectionString">The SQL Server test container's connection string.</param>
/// <param name="redisConnectionString">The Redis test container's connection string.</param>
/// <param name="rateLimitingSettings">
/// <c>RateLimiting</c> configuration overrides. When <c>null</c>, rate limiting is switched off so
/// only the tests that opt in are affected by it.
/// </param>
/// <param name="requestTimeoutsSettings">
/// <c>RequestTimeouts</c> configuration overrides. When <c>null</c>, the values from
/// <c>appsettings.json</c> apply.
/// </param>
public sealed class ApiFactory(
    string connectionString,
    string redisConnectionString,
    IReadOnlyDictionary<string, string?>? rateLimitingSettings = null,
    IReadOnlyDictionary<string, string?>? requestTimeoutsSettings = null) : WebApplicationFactory<Program>
{
    public const string EnvironmentName = "Testing";

    // Base64 of 32 bytes; only ever used by tests.
    public const string TestJwtSigningKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

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
                ["ConnectionStrings:Redis"] = redisConnectionString,
                ["Jwt:SigningKey"] = TestJwtSigningKey,
                ["App:FrontendBaseUrl"] = "https://localhost:4200",
            });

            if (rateLimitingSettings is not null)
            {
                configuration.AddInMemoryCollection(rateLimitingSettings);
            }

            if (requestTimeoutsSettings is not null)
            {
                configuration.AddInMemoryCollection(requestTimeoutsSettings);
            }
        });
    }
}