namespace Liftingo.Application.Common.Security;

/// <summary>The outcome of presenting a refresh token to <see cref="ISessionStore.RotateAsync"/>.</summary>
public abstract record RotationResult
{
    // Closed hierarchy: only the nested cases below derive from it.
    private RotationResult()
    {
    }

    /// <summary>The token was current. <c>RemainingLifetime</c> is the session's TTL after renewal.</summary>
    public sealed record Rotated(SessionInfo Session, string NewRefreshToken, TimeSpan RemainingLifetime) : RotationResult;

    /// <summary>The token had been used already. The session has been revoked.</summary>
    public sealed record Reused(SessionInfo Session) : RotationResult;

    /// <summary>The token is unknown, or its session is gone.</summary>
    public sealed record Invalid : RotationResult;
}