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
        Factory.Time.SetUtcNow(DateTimeOffset.UtcNow);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    /// <summary>Runs <paramref name="action"/> with a fresh <see cref="ApplicationDbContext"/> from the host.</summary>
    protected async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
}
