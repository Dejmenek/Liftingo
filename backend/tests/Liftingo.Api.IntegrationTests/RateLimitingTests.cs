using Liftingo.Api.IntegrationTests.Infrastructure;

using Serilog.Events;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Liftingo.Api.IntegrationTests;

public class RateLimitingTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ProblemJson = "application/problem+json";
    private const int AnonymousLimit = 3;
    private const int AuthenticatedLimit = 5;

    [Fact]
    public async Task Request_AnonymousBucketExhausted_Returns429WithRetryAfterAndProblemDetails()
    {
        using ApiFactory factory = CreateLimitedFactory();
        using HttpClient client = factory.CreateClient();

        await SendManyAsync(client, AnonymousLimit, clientIp: "203.0.113.1");
        using HttpResponseMessage response = await SendAsync(client, clientIp: "203.0.113.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? retryAfter));
        Assert.True(int.Parse(retryAfter.Single(), CultureInfo.InvariantCulture) > 0);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((int)HttpStatusCode.TooManyRequests, body.GetProperty("status").GetInt32());
        Assert.Equal("RateLimit.Exceeded", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Request_AnonymousBucketNotExhausted_ReturnsSuccess()
    {
        using ApiFactory factory = CreateLimitedFactory();
        using HttpClient client = factory.CreateClient();

        HttpStatusCode[] statuses = await SendManyAsync(client, AnonymousLimit, clientIp: "203.0.113.1");

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task Request_DifferentClientIps_UseSeparateBuckets()
    {
        using ApiFactory factory = CreateLimitedFactory();
        using HttpClient client = factory.CreateClient();
        await SendManyAsync(client, AnonymousLimit, clientIp: "203.0.113.1");

        using HttpResponseMessage exhausted = await SendAsync(client, clientIp: "203.0.113.1");
        using HttpResponseMessage other = await SendAsync(client, clientIp: "203.0.113.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, exhausted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task Request_NoRemoteIp_SharesOneBucket()
    {
        using ApiFactory factory = CreateLimitedFactory();
        using HttpClient client = factory.CreateClient();
        await SendManyAsync(client, AnonymousLimit);

        using HttpResponseMessage response = await SendAsync(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Request_Authenticated_UsesUserBucketWithAuthenticatedLimit()
    {
        using ApiFactory factory = CreateLimitedFactory();
        using HttpClient client = factory.CreateClient();
        string userId = Guid.NewGuid().ToString();

        HttpStatusCode[] allowed = await SendManyAsync(client, AuthenticatedLimit, clientIp: "203.0.113.1", userId: userId);
        using HttpResponseMessage rejected = await SendAsync(client, clientIp: "203.0.113.1", userId: userId);

        Assert.All(allowed, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task Request_DifferentUsersOnSameIp_UseSeparateBuckets()
    {
        using ApiFactory factory = CreateLimitedFactory();
        using HttpClient client = factory.CreateClient();
        await SendManyAsync(client, AuthenticatedLimit, clientIp: "203.0.113.1", userId: Guid.NewGuid().ToString());

        using HttpResponseMessage otherUser = await SendAsync(client, clientIp: "203.0.113.1", userId: Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);
    }

    [Fact]
    public async Task Request_AuthenticatedUserExhausted_DoesNotConsumeAnonymousBucketOfSameIp()
    {
        using ApiFactory factory = CreateLimitedFactory();
        using HttpClient client = factory.CreateClient();
        string userId = Guid.NewGuid().ToString();
        await SendManyAsync(client, AuthenticatedLimit + 1, clientIp: "203.0.113.1", userId: userId);

        using HttpResponseMessage anonymous = await SendAsync(client, clientIp: "203.0.113.1");

        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
    }

    [Fact]
    public async Task Request_Rejected_LogsPartitionTypeAndPolicyWithoutIpAddress()
    {
        const string clientIp = "203.0.113.77";
        using ApiFactory factory = CreateLimitedFactory();
        using HttpClient client = factory.CreateClient();
        await SendManyAsync(client, AnonymousLimit, clientIp);
        factory.Logs.Clear();

        using HttpResponseMessage response = await SendAsync(client, clientIp);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        LogEvent logged = Assert.Single(factory.Logs.Events, e => e.Level == LogEventLevel.Warning);
        Assert.Equal("Ip", logged.Properties["PartitionType"].ToString().Trim('"'));
        Assert.Equal("Anonymous", logged.Properties["Policy"].ToString().Trim('"'));
        Assert.DoesNotContain(factory.Logs.Events, e =>
            e.RenderMessage(CultureInfo.InvariantCulture).Contains(clientIp, StringComparison.Ordinal)
            || e.Properties.Values.Any(v => v.ToString().Contains(clientIp, StringComparison.Ordinal)));
    }

    private ApiFactory CreateLimitedFactory() =>
        new(Fixture.ConnectionString, new Dictionary<string, string?>
        {
            ["RateLimiting:Authenticated:TokenLimit"] = AuthenticatedLimit.ToString(CultureInfo.InvariantCulture),
            ["RateLimiting:Authenticated:TokensPerPeriod"] = "1",
            ["RateLimiting:Authenticated:ReplenishmentPeriod"] = "01:00:00",
            ["RateLimiting:Anonymous:TokenLimit"] = AnonymousLimit.ToString(CultureInfo.InvariantCulture),
            ["RateLimiting:Anonymous:TokensPerPeriod"] = "1",
            ["RateLimiting:Anonymous:ReplenishmentPeriod"] = "01:00:00",
        });

    private static async Task<HttpStatusCode[]> SendManyAsync(HttpClient client, int count, string? clientIp = null, string? userId = null)
    {
        List<HttpStatusCode> statuses = [];

        for (int i = 0; i < count; i++)
        {
            using HttpResponseMessage response = await SendAsync(client, clientIp, userId);

            statuses.Add(response.StatusCode);
        }

        return [.. statuses];
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string? clientIp = null, string? userId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, TestEndpointsStartupFilter.GetOnlyPath);

        if (clientIp is not null)
        {
            request.Headers.Add(TestEndpointsStartupFilter.ClientIpHeader, clientIp);
        }

        if (userId is not null)
        {
            request.Headers.Add(TestEndpointsStartupFilter.UserIdHeader, userId);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}