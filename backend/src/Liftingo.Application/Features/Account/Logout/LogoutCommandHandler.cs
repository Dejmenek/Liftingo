using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.Logout;

/// <summary>
/// Ends the session of the refresh token. It works with the cookie alone, so a user whose access token has
/// expired can still sign out.
/// </summary>
public sealed class LogoutCommandHandler(ISessionStore sessionStore, SecurityAudit audit) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> HandleAsync(LogoutCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        byte[]? tokenHash = command.RefreshToken is { } token ? OpaqueToken.Hash(token) : null;

        // Idempotent: a missing, unknown or already revoked token is not an error and writes no audit record.
        if (tokenHash is not null && await sessionStore.RevokeByTokenAsync(tokenHash, ct) is { } revoked)
        {
            await audit.WriteAsync(
                revoked.UserId,
                SecurityEventType.Logout,
                SecurityEventOutcome.Succeeded,
                command.Client with { DeviceType = revoked.Device.DeviceType },
                ct);
        }

        return Result.Success();
    }
}