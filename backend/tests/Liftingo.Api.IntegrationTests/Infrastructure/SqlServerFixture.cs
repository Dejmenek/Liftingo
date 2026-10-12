using Liftingo.Infrastructure.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Respawn;

using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // Same tag as backend/docker-compose.yml.
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:latest").Build();

    private Respawner? _respawner;

    public ApiFactory Factory { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

    public string RedisConnectionString => _redisContainer.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_container.StartAsync(), _redisContainer.StartAsync());

        await MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")],
        });

        Factory = new ApiFactory(ConnectionString, RedisConnectionString);
    }

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException("The fixture has not been initialized.");
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _container.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    private async Task MigrateAsync()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        await dbContext.Database.MigrateAsync();
    }
}