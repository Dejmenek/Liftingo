using Liftingo.Infrastructure.Audit;
using Liftingo.Infrastructure.Email;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

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

    // Frontend and legal values the tests rely on.
    public const string FrontendBaseUrl = "https://localhost:4200";
    public const string LegalDocumentsVersion = "2026-10-01-test";

    public LogEventCollector Logs { get; } = new();

    /// <summary>The clock of the whole host (tokens, sessions, e-mail limits). Starts near real UTC now.</summary>
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public CapturingEmailTransport Emails { get; } = new();

    public CapturingAuditSink Audit { get; } = new();

    /// <summary>
    /// A client for <c>https://localhost</c>, so <c>Secure</c> cookies are stored and sent. With
    /// <paramref name="handleCookies"/> it behaves like one browser (one device).
    /// </summary>
    public HttpClient CreateApiClient(bool handleCookies = true) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies,
            AllowAutoRedirect = false,
        });

    /// <summary>A new "device": its own cookie jar, calling the auth API.</summary>
    public AuthTestClient CreateAuthClient() => new(CreateApiClient);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        builder.ConfigureServices(services =>
        {
            services.AddTestEndpoints(Logs);

            // Replace the registrations (not add): the app registers the real ones.
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<IEmailTransport>();
            services.AddSingleton<IEmailTransport>(Emails);
            services.RemoveAll<IAuditSink>();
            services.AddSingleton<IAuditSink>(Audit);

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
                ["App:FrontendBaseUrl"] = FrontendBaseUrl,
                ["Legal:DocumentsVersion"] = LegalDocumentsVersion,
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