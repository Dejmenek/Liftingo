using Liftingo.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollectionDefinition.Name)]
public abstract class IntegrationTestBase(SqlServerFixture fixture) : IAsyncLifetime
{
    protected SqlServerFixture Fixture { get; } = fixture;

    protected ApiFactory Factory => Fixture.Factory;

    public async ValueTask InitializeAsync()
    {
        await Fixture.ResetDatabaseAsync();
        await Fixture.FlushRedisAsync();

        // The shared host keeps its singletons between tests; start each test from a clean slate.
        Factory.Emails.Clear();
        Factory.Audit.Clear();
        Factory.Logs.Clear();

        // FakeTimeProvider cannot go back. A test that advanced the shared clock would break every later test, so
        // tests that move the clock use their own host (CreateIsolatedFactory); this only keeps the clock current.
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (Factory.Time.GetUtcNow() < now)
        {
            Factory.Time.SetUtcNow(now);
        }
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A host of its own, with its own clock, e-mail and audit capture. Use it in tests that advance
    /// <see cref="ApiFactory.Time"/>: the shared host's clock must never move.
    /// </summary>
    protected ApiFactory CreateIsolatedFactory() => new(Fixture.ConnectionString, Fixture.RedisConnectionString);

    /// <summary>Runs <paramref name="action"/> with a fresh <see cref="ApplicationDbContext"/> from the host.</summary>
    protected async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
}