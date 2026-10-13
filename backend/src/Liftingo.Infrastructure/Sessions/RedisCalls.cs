using System.Reflection;

using Liftingo.Application.Common.Exceptions;

using StackExchange.Redis;

namespace Liftingo.Infrastructure.Sessions;

/// <summary>What every Redis-backed store here shares: the embedded Lua scripts and the outage mapping.</summary>
internal static class RedisCalls
{
    // Redis connection and timeout errors become one exception that Api maps to 503 (FR-015c).
    public static async Task<T> GuardAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (RedisConnectionException ex)
        {
            throw new SessionStoreUnavailableException("The session store cannot be reached.", ex);
        }
        catch (RedisTimeoutException ex)
        {
            throw new SessionStoreUnavailableException("The session store did not answer in time.", ex);
        }
    }

    public static string LoadScript(string name)
    {
        Assembly assembly = typeof(RedisCalls).Assembly;

        using Stream stream = assembly.GetManifestResourceStream($"{typeof(RedisCalls).Namespace}.Scripts.{name}")
            ?? throw new InvalidOperationException($"Embedded script '{name}' was not found.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
