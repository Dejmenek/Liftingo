using Liftingo.Application.Common.Exceptions;
using Liftingo.Infrastructure.Sessions;

using StackExchange.Redis;

namespace Liftingo.Api.UnitTests.Infrastructure.Sessions;

public class RedisCallsTests
{
    [Fact]
    public async Task GuardAsync_ClientCancelsWithoutTheCaller_ThrowsSessionStoreUnavailable()
    {
        SessionStoreUnavailableException exception = await Assert.ThrowsAsync<SessionStoreUnavailableException>(
            () => RedisCalls.GuardAsync<bool>(() => throw new TaskCanceledException(), CancellationToken.None));

        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task GuardAsync_CallerCancelled_LetsTheCancellationThrough()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => RedisCalls.GuardAsync<bool>(() => throw new TaskCanceledException(), cancellation.Token));
    }

    [Fact]
    public async Task GuardAsync_ConnectionFailure_ThrowsSessionStoreUnavailable()
    {
        await Assert.ThrowsAsync<SessionStoreUnavailableException>(
            () => RedisCalls.GuardAsync<bool>(
                () => throw new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect, CommandFlags.None, "down", innerException: null, CommandStatus.Unknown),
                CancellationToken.None));
    }

    [Fact]
    public async Task GuardAsync_Timeout_ThrowsSessionStoreUnavailable()
    {
        await Assert.ThrowsAsync<SessionStoreUnavailableException>(
            () => RedisCalls.GuardAsync<bool>(
                () => throw new RedisTimeoutException(CommandFlags.None, "slow", CommandStatus.Sent),
                CancellationToken.None));
    }

    [Fact]
    public async Task GuardAsync_Success_ReturnsTheResult()
    {
        int result = await RedisCalls.GuardAsync(() => Task.FromResult(42), CancellationToken.None);

        Assert.Equal(42, result);
    }
}
