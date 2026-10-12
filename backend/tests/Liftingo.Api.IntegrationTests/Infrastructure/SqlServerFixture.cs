using Liftingo.Infrastructure.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Respawn;

using StackExchange.Redis;

using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace Liftingo.Api.IntegrationTests.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // Same tag as backend/docker-compose.yml.
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:latest").Build();

    private Respawner? _respawner;

    private ConnectionMultiplexer? _redisAdmin;

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
            // The key ring must survive resets: the app caches it, and wrapped audit keys depend on it.
            TablesToIgnore =
            [
                new Respawn.Graph.Table("__EFMigrationsHistory"),
                new Respawn.Graph.Table("DataProtectionKeys"),
            ],
        });

        // FLUSHALL is an admin command.
        ConfigurationOptions redisOptions = ConfigurationOptions.Parse(RedisConnectionString);
        redisOptions.AllowAdmin = true;
        _redisAdmin = await ConnectionMultiplexer.ConnectAsync(redisOptions);

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

    /// <summary>Removes every key (sessions, refresh tokens), so no test sees another test's sessions.</summary>
    public async Task FlushRedisAsync()
    {
        if (_redisAdmin is null)
        {
            throw new InvalidOperationException("The fixture has not been initialized.");
        }

        foreach (System.Net.EndPoint endpoint in _redisAdmin.GetEndPoints())
        {
            await _redisAdmin.GetServer(endpoint).FlushAllDatabasesAsync();
        }
    }

    /// <summary>Freezes the Redis container: connections stay open but nothing answers (a store outage).</summary>
    public Task PauseRedisAsync() => _redisContainer.PauseAsync();

    public Task UnpauseRedisAsync() => _redisContainer.UnpauseAsync();

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_redisAdmin is not null)
        {
            await _redisAdmin.DisposeAsync();
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