using Liftingo.Application.Common.Exceptions;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Buffers.Text;
using System.Globalization;

namespace Liftingo.Infrastructure.Sessions;

/// <summary>
/// <see cref="ISessionStore"/> over Redis. Key layout (all under <c>liftingo:</c>):
/// <c>session:{id}</c> hash, <c>rt:{tokenHash}</c> hash, <c>user-sessions:{userId}</c> set.
/// <c>absoluteExpiresAt</c> is stored as Unix milliseconds so the scripts can do the arithmetic;
/// <c>createdAt</c> and <c>lastRenewedAt</c> are ISO 8601.
/// </summary>
public sealed class RedisSessionStore(
    IConnectionMultiplexer redis,
    IOptions<SessionsOptions> options,
    TimeProvider timeProvider) : ISessionStore
{
    private const string Prefix = "liftingo:";

    private static readonly string s_rotateScript = RedisCalls.LoadScript("rotate.lua");
    private static readonly string s_revokeScript = RedisCalls.LoadScript("revoke.lua");
    private static readonly string s_revokeByTokenScript = RedisCalls.LoadScript("revoke-by-token.lua");
    private static readonly string s_revokeAllScript = RedisCalls.LoadScript("revoke-all.lua");

    public async Task<(Guid SessionId, string RefreshToken, TimeSpan RemainingLifetime)> CreateAsync(
        Guid userId,
        SessionDevice device,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(device);

        return await RedisCalls.GuardAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            SessionsOptions sessions = options.Value;
            DateTimeOffset now = timeProvider.GetUtcNow();
            Guid sessionId = Guid.NewGuid();
            (string refreshToken, byte[] tokenHash) = OpaqueToken.Generate();
            string hash = Base64Url.EncodeToString(tokenHash);

            // The absolute lifetime is never shorter than the inactivity timeout (options validator).
            TimeSpan ttl = sessions.InactivityTimeout;

            RedisKey sessionKey = SessionKey(sessionId);
            RedisKey rtKey = RefreshTokenKey(hash);
            RedisKey userKey = UserSessionsKey(userId);

            ITransaction transaction = redis.GetDatabase().CreateTransaction();

            _ = transaction.HashSetAsync(sessionKey,
            [
                new HashEntry("userId", userId.ToString()),
                new HashEntry("deviceId", device.DeviceId.ToString()),
                new HashEntry("deviceType", device.DeviceType),
                new HashEntry("createdAt", now.ToString("O", CultureInfo.InvariantCulture)),
                new HashEntry("absoluteExpiresAt", (now + sessions.AbsoluteLifetime).ToUnixTimeMilliseconds()),
                new HashEntry("lastRenewedAt", now.ToString("O", CultureInfo.InvariantCulture)),
                new HashEntry("currentTokenHash", hash),
            ]);
            _ = transaction.KeyExpireAsync(sessionKey, ttl);
            _ = transaction.HashSetAsync(rtKey,
            [
                new HashEntry("sessionId", sessionId.ToString()),
                new HashEntry("state", "active"),
            ]);
            _ = transaction.KeyExpireAsync(rtKey, ttl);
            _ = transaction.SetAddAsync(userKey, sessionId.ToString());
            _ = transaction.KeyExpireAsync(userKey, ttl);

            if (!await transaction.ExecuteAsync())
            {
                throw new SessionStoreUnavailableException("The session transaction was not committed.");
            }

            return (sessionId, refreshToken, ttl);
        }, ct);
    }

    public async Task<RotationResult> RotateAsync(byte[] tokenHash, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        return await RedisCalls.GuardAsync<RotationResult>(async () =>
        {
            ct.ThrowIfCancellationRequested();

            DateTimeOffset now = timeProvider.GetUtcNow();
            (string newToken, byte[] newHash) = OpaqueToken.Generate();

            RedisResult raw = await redis.GetDatabase().ScriptEvaluateAsync(
                s_rotateScript,
                [RefreshTokenKey(Base64Url.EncodeToString(tokenHash))],
                [
                    Prefix,
                    Base64Url.EncodeToString(newHash),
                    now.ToUnixTimeMilliseconds(),
                    (long)options.Value.InactivityTimeout.TotalMilliseconds,
                    now.ToString("O", CultureInfo.InvariantCulture),
                ]);

            RedisResult[] result = (RedisResult[]?)raw ?? throw new InvalidOperationException("The rotate script returned nothing.");

            switch ((int)result[0])
            {
                case 1:
                    return new RotationResult.Rotated(
                        ToSessionInfo(result[4], result[1], result[2], result[3]),
                        newToken,
                        TimeSpan.FromMilliseconds((long)result[5]));
                case 2:
                    return new RotationResult.Reused(ToSessionInfo(result[4], result[1], result[2], result[3]));
                default:
                    return new RotationResult.Invalid();
            }
        }, ct);
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken ct)
    {
        await RedisCalls.GuardAsync<bool>(async () =>
        {
            ct.ThrowIfCancellationRequested();

            _ = await redis.GetDatabase().ScriptEvaluateAsync(
                s_revokeScript,
                [SessionKey(sessionId)],
                [Prefix, sessionId.ToString()]);

            return true;
        }, ct);
    }

    public async Task<SessionInfo?> RevokeByTokenAsync(byte[] tokenHash, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        return await RedisCalls.GuardAsync<SessionInfo?>(async () =>
        {
            ct.ThrowIfCancellationRequested();

            RedisResult raw = await redis.GetDatabase().ScriptEvaluateAsync(
                s_revokeByTokenScript,
                [RefreshTokenKey(Base64Url.EncodeToString(tokenHash))],
                [Prefix]);

            RedisResult[]? result = raw.IsNull ? null : (RedisResult[]?)raw;

            return result is { Length: > 0 }
                ? ToSessionInfo(result[3], result[0], result[1], result[2])
                : null;
        }, ct);
    }

    public async Task<int> RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        return await RedisCalls.GuardAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            RedisResult result = await redis.GetDatabase().ScriptEvaluateAsync(
                s_revokeAllScript,
                [UserSessionsKey(userId)],
                [Prefix]);

            return (int)result;
        }, ct);
    }

    public async Task<bool> IsActiveAsync(Guid sessionId, CancellationToken ct)
    {
        return await RedisCalls.GuardAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            return await redis.GetDatabase().KeyExistsAsync(SessionKey(sessionId));
        }, ct);
    }

    private static SessionInfo ToSessionInfo(RedisResult sessionId, RedisResult userId, RedisResult deviceId, RedisResult deviceType) =>
        new(
            Guid.Parse(Text(sessionId)),
            Guid.Parse(Text(userId)),
            new SessionDevice(Guid.Parse(Text(deviceId)), Text(deviceType)));

    private static string Text(RedisResult value) =>
        value.ToString() ?? throw new InvalidOperationException("A session script returned an empty field.");

    private static RedisKey SessionKey(Guid sessionId) => Prefix + "session:" + sessionId;

    private static RedisKey RefreshTokenKey(string tokenHash) => Prefix + "rt:" + tokenHash;

    private static RedisKey UserSessionsKey(Guid userId) => Prefix + "user-sessions:" + userId;
}