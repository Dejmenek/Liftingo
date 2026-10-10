using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Application.Common.Result;
using Microsoft.AspNetCore.Hosting;
using Serilog.Events;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Liftingo.Api.IntegrationTests;

public class ExceptionHandlingTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task DomainException_Thrown_Returns422WithMessageAsDetail()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, TestEndpointsStartupFilter.DomainExceptionPath, TestContext.Current.CancellationToken);

        JsonElement body = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        Assert.Equal(TestEndpointsStartupFilter.ExceptionMessage, body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task DomainException_Thrown_LogsWarningAndNoError()
    {
        Factory.Logs.Clear();
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, TestEndpointsStartupFilter.DomainExceptionPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(Factory.Logs.Events, e => e.Level == LogEventLevel.Warning);
        Assert.DoesNotContain(Factory.Logs.Events, e => e.Level >= LogEventLevel.Error);
        Assert.DoesNotContain(Factory.Logs.Events, e => e.Exception is not null);
    }

    [Fact]
    public async Task UnhandledException_Thrown_Returns500WithoutMessageOutsideDevelopment()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, TestEndpointsStartupFilter.UnhandledExceptionPath, TestContext.Current.CancellationToken);

        string raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        JsonElement body = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(TestEndpointsStartupFilter.SecretMessage, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", raw, StringComparison.OrdinalIgnoreCase);
        Assert.False(body.TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task UnhandledException_InDevelopment_IncludesExceptionDetails()
    {
        await using WebApplicationFactoryScope development = new(Factory, "Development");
        using HttpClient client = development.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, TestEndpointsStartupFilter.UnhandledExceptionPath, TestContext.Current.CancellationToken);

        JsonElement body = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        JsonElement exception = body.GetProperty("exception");
        Assert.Equal(typeof(InvalidOperationException).FullName, exception.GetProperty("type").GetString());
        Assert.Equal(TestEndpointsStartupFilter.SecretMessage, exception.GetProperty("message").GetString());
        Assert.False(string.IsNullOrEmpty(exception.GetProperty("stackTrace").GetString()));
    }

    [Fact]
    public async Task UnhandledException_Thrown_LogsExactlyOneErrorWithException()
    {
        Factory.Logs.Clear();
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, TestEndpointsStartupFilter.UnhandledExceptionPath, TestContext.Current.CancellationToken);

        JsonElement body = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        LogEvent logged = Assert.Single(Factory.Logs.Events, e => e.Level >= LogEventLevel.Error || e.Exception is not null);
        Assert.Equal(LogEventLevel.Error, logged.Level);
        Assert.IsType<InvalidOperationException>(logged.Exception);
        Assert.Contains(body.GetProperty("traceId").GetString()!, logged.RenderMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ErrorType.Failure, HttpStatusCode.BadRequest)]
    [InlineData(ErrorType.NotFound, HttpStatusCode.NotFound)]
    [InlineData(ErrorType.Conflict, HttpStatusCode.Conflict)]
    [InlineData(ErrorType.Unauthorized, HttpStatusCode.Unauthorized)]
    public async Task FailedResult_Returned_MapsToStatusWithCodeAndDetail(ErrorType type, HttpStatusCode expectedStatus)
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, $"{TestEndpointsStartupFilter.ResultPath}/{type}", TestContext.Current.CancellationToken);

        JsonElement body = await AssertProblemAsync(response, expectedStatus);
        Assert.Equal("Test.Error", body.GetProperty("code").GetString());
        Assert.Equal("Test description", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task FailedResult_WithValidationError_Returns400WithErrorsArray()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, TestEndpointsStartupFilter.ValidationResultPath, TestContext.Current.CancellationToken);

        JsonElement body = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Validation.General", body.GetProperty("code").GetString());
        JsonElement[] errors = [.. body.GetProperty("errors").EnumerateArray()];
        Assert.Equal(2, errors.Length);
        Assert.Equal("Name.Empty", errors[0].GetProperty("code").GetString());
        Assert.Equal("Age must not be negative", errors[1].GetProperty("description").GetString());
    }

    [Fact]
    public async Task UnknownRoute_Requested_Returns404ProblemDetails()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, "/test/does-not-exist", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WrongMethod_Requested_Returns405ProblemDetails()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(new Uri(TestEndpointsStartupFilter.GetOnlyPath, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, CancellationToken cancellationToken) =>
        client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((int)expectedStatus, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));

        return body;
    }

    private sealed class WebApplicationFactoryScope(ApiFactory factory, string environment) : IAsyncDisposable
    {
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory =
            factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));

        public HttpClient CreateClient() => _factory.CreateClient();

        public ValueTask DisposeAsync() => _factory.DisposeAsync();
    }
}