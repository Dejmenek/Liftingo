using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Liftingo.Api.IntegrationTests;

public class ApiStartupSmokeTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public void Startup_ConnectionStringOverridden_UsesContainerConnectionString()
    {
        using HttpClient client = Factory.CreateClient();

        var options = Factory.Services.GetRequiredService<IOptions<ConnectionStringsOptions>>();

        Assert.Equal(Fixture.ConnectionString, options.Value.Default);
    }
}