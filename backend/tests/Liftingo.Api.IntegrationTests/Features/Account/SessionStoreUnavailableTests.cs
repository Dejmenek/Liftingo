using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Liftingo.Api.Endpoints;
using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Domain.Account;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Liftingo.Infrastructure.Persistence;

namespace Liftingo.Api.IntegrationTests.Features.Account;

[CollectionDefinition(Name)]
public sealed class SessionStoreUnavailableCollectionDefinition : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SessionStoreUnavailable";
}

/// <summary>
/// Redis is paused during these tests, so the class has a collection and fixture (containers) of its own and
/// no other test class shares the Redis it freezes.
/// </summary>
[Collection(SessionStoreUnavailableCollectionDefinition.Name)]
public class SessionStoreUnavailableTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Password = "Sztanga1!";

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        await fixture.FlushRedisAsync();
        fixture.Factory.Emails.Clear();
        fixture.Factory.Audit.Clear();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Login_RedisPaused_Returns503ServiceUnavailableWithRetryAfter()
    {
        ApiFactory factory = fixture.Factory;
        using AuthTestClient client = factory.CreateAuthClient();
        (HttpResponseMessage registered, var request) = await client.RegisterAsync(password: Password);
        registered.Dispose();

        await WhileRedisPausedAsync(async () =>
        {
            using HttpResponseMessage response = await client.LoginAsync(request.Email, Password);

            await AssertUnavailableAsync(response);
        });
    }

    [Fact]
    public async Task Register_RedisPaused_Returns503AndTheAccountAndConsentsAreKept()
    {
        ApiFactory factory = fixture.Factory;
        using AuthTestClient client = factory.CreateAuthClient();
        string email = $"redis-down-{Guid.NewGuid():N}@example.pl";

        await WhileRedisPausedAsync(async () =>
        {
            (HttpResponseMessage response, _) = await client.RegisterAsync(email, Password);
            using HttpResponseMessage owned = response;

            await AssertUnavailableAsync(response);
        });

        // SQL is committed before Redis is written (R2): nothing is rolled back.
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid userId = await db.Users
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, await db.UserConsents.CountAsync(c => c.UserId == userId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refresh_RedisPaused_Returns503ServiceUnavailableWithRetryAfter()
    {
        ApiFactory factory = fixture.Factory;
        using AuthTestClient client = factory.CreateAuthClient();
        (HttpResponseMessage registered, _) = await client.RegisterAsync(password: Password);
        registered.Dispose();

        await WhileRedisPausedAsync(async () =>
        {
            using HttpResponseMessage response = await client.RefreshAsync();

            await AssertUnavailableAsync(response);
        });
    }

    [Fact]
    public async Task GetMe_ValidBearerAndRedisPaused_Returns503ServiceUnavailableWithRetryAfter()
    {
        ApiFactory factory = fixture.Factory;
        using AuthTestClient client = factory.CreateAuthClient();
        (HttpResponseMessage registered, _) = await client.RegisterAsync(password: Password);
        using HttpResponseMessage owned = registered;
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(registered);

        await WhileRedisPausedAsync(async () =>
        {
            using HttpResponseMessage response = await client.GetMeAsync(session.AccessToken);

            await AssertUnavailableAsync(response);
        });

        // Redis is back: the same token works again.
        using HttpResponseMessage after = await client.GetMeAsync(session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    private async Task WhileRedisPausedAsync(Func<Task> action)
    {
        await fixture.PauseRedisAsync();

        try
        {
            await action();
        }
        finally
        {
            await fixture.UnpauseRedisAsync();
        }
    }

    private static async Task AssertUnavailableAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(30), response.Headers.RetryAfter?.Delta);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Service.Unavailable", body.GetProperty("code").GetString());
    }
}
