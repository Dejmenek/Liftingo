using Liftingo.Api.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Liftingo.Api.IntegrationTests;

public class ApiVersioningTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Ping_V1Route_ReturnsOk()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/v1/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ping_V1Route_ReportsSupportedVersions()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/v1/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("api-supported-versions", out IEnumerable<string>? supported));
        Assert.Equal("1.0", supported.Single());
    }

    [Fact]
    public async Task OpenApi_V1DocumentInDevelopment_ContainsVersionedPaths()
    {
        using WebApplicationFactory<Program> development = Factory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Development));
        using HttpClient client = development.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/v1/ping", out _));
    }

    [Fact]
    public async Task Ping_UnversionedRoute_IsNotFound()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Request_UnknownVersion_ReturnsProblemDetailsInsteadOfServerError()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/v99/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((int)HttpStatusCode.NotFound, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }
}