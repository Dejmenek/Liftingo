namespace Liftingo.Application.Common.Security;

/// <summary>
/// Refresh-token sessions. Every method throws <see cref="Exceptions.SessionStoreUnavailableException"/>
/// when the store cannot be reached.
/// </summary>
public interface ISessionStore
{
    Task<(Guid SessionId, string RefreshToken, TimeSpan RemainingLifetime)> CreateAsync(
        Guid userId,
        SessionDevice device,
        CancellationToken ct);

    Task<RotationResult> RotateAsync(byte[] tokenHash, CancellationToken ct);

    Task RevokeAsync(Guid sessionId, CancellationToken ct);

    /// <returns>The revoked session, or <c>null</c> when the token matches no active session.</returns>
    Task<SessionInfo?> RevokeByTokenAsync(byte[] tokenHash, CancellationToken ct);

    /// <returns>How many sessions were revoked.</returns>
    Task<int> RevokeAllAsync(Guid userId, CancellationToken ct);

    Task<bool> IsActiveAsync(Guid sessionId, CancellationToken ct);
}
