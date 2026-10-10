using Liftingo.Api.IntegrationTests.Infrastructure;

using Serilog.Events;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Liftingo.Api.IntegrationTests;

public class RequestTimeoutTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Request_ExceedsDefaultTimeout_Returns504WithProblemDetails()
    {
        using ApiFactory factory = CreateShortTimeoutFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, TestEndpointsStartupFilter.SlowPath);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((int)HttpStatusCode.GatewayTimeout, body.GetProperty("status").GetInt32());
        Assert.Equal("Request.Timeout", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Request_TimedOut_LogsWarningWithPolicyAndNoUnhandledExceptionOrQueryString()
    {
        using ApiFactory factory = CreateShortTimeoutFactory();
        using HttpClient client = factory.CreateClient();
        factory.Logs.Clear();

        using HttpResponseMessage response = await GetAsync(client, $"{TestEndpointsStartupFilter.SlowPath}?token=secret-query-value");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        LogEvent logged = Assert.Single(factory.Logs.Events, e => e.Level == LogEventLevel.Warning && e.Properties.ContainsKey("Policy"));
        Assert.Equal("Default", logged.Properties["Policy"].ToString().Trim('"'));
        Assert.DoesNotContain(factory.Logs.Events, e => e.Level >= LogEventLevel.Error && e.Exception is not null);
        Assert.DoesNotContain(factory.Logs.Events, e =>
            e.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? source)
            && source.ToString().Contains("RequestTimeoutsMiddleware", StringComparison.Ordinal));
        Assert.DoesNotContain(factory.Logs.Events, e =>
            e.RenderMessage(CultureInfo.InvariantCulture).Contains("secret-query-value", StringComparison.Ordinal)
            || e.Properties.Values.Any(v => v.ToString().Contains("secret-query-value", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Request_NamedPolicyLongerThanDefault_Completes()
    {
        using ApiFactory factory = CreateShortTimeoutFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await GetAsync(client, TestEndpointsStartupFilter.SlowNamedPolicyPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_NamedPolicyExceeded_Returns504AndLogsPolicyName()
    {
        using ApiFactory factory = CreateFactory(defaultTimeout: "00:00:30", namedTimeout: "00:00:00.3");
        using HttpClient client = factory.CreateClient();
        factory.Logs.Clear();

        using HttpResponseMessage response = await GetAsync(client, TestEndpointsStartupFilter.SlowNamedPolicyPath);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        LogEvent logged = Assert.Single(factory.Logs.Events, e => e.Level == LogEventLevel.Warning && e.Properties.ContainsKey("Policy"));
        Assert.Equal(TestEndpointsStartupFilter.SlowNamedPolicyName, logged.Properties["Policy"].ToString().Trim('"'));
    }

    private ApiFactory CreateShortTimeoutFactory() => CreateFactory(defaultTimeout: "00:00:00.3", namedTimeout: "00:00:30");

    private ApiFactory CreateFactory(string defaultTimeout, string namedTimeout) =>
        new(Fixture.ConnectionString, requestTimeoutsSettings: new Dictionary<string, string?>
        {
            ["RequestTimeouts:DefaultTimeout"] = defaultTimeout,
            [$"RequestTimeouts:Policies:{TestEndpointsStartupFilter.SlowNamedPolicyName}"] = namedTimeout,
        });

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path) =>
        client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
}