namespace Liftingo.Api.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollectionDefinition.Name)]
public abstract class IntegrationTestBase(SqlServerFixture fixture) : IAsyncLifetime
{
    protected SqlServerFixture Fixture { get; } = fixture;

    protected ApiFactory Factory => Fixture.Factory;

    public async ValueTask InitializeAsync() => await Fixture.ResetDatabaseAsync();

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}