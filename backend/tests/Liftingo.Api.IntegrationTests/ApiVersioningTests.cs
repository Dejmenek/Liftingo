using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Liftingo.Api.IntegrationTests;

// The routing tests use GET /auth/me without a bearer: a route that exists answers 401, not 404.
public class ApiVersioningTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Me_V1RouteWithoutBearer_ReturnsUnauthorizedNotNotFound()
    {
        using HttpClient client = Factory.CreateApiClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_V1RouteWithBearer_ReportsSupportedVersions()
    {
        // The versions are reported by the endpoint itself, so the answer must come from the endpoint:
        // an anonymous call is stopped by the authorization middleware before it gets there.
        using AuthTestClient client = Factory.CreateAuthClient();
        (HttpResponseMessage registered, _) = await client.RegisterAsync();
        using HttpResponseMessage registeredResponse = registered;
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(registeredResponse);

        using HttpResponseMessage response = await client.GetMeAsync(session.AccessToken);

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
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/v1/auth/me", out _));
    }

    [Fact]
    public async Task Me_UnversionedRoute_IsNotFound()
    {
        using HttpClient client = Factory.CreateApiClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/auth/me", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Request_UnknownVersion_ReturnsProblemDetailsInsteadOfServerError()
    {
        using HttpClient client = Factory.CreateApiClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/v99/auth/me", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((int)HttpStatusCode.NotFound, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }
}
