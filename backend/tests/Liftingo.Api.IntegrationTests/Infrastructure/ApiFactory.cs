using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string EnvironmentName = "Testing";

    public LogEventCollector Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        builder.ConfigureServices(services => services.AddTestEndpoints(Logs));

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
            }));
    }
}