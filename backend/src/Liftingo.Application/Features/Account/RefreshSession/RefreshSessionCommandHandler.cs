using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.RefreshSession;

/// <summary>Rotates the refresh token (FR-009) and detects reuse of a rotated one (FR-011). A command: it writes to Redis.</summary>
public sealed class RefreshSessionCommandHandler(
    ISessionStore sessionStore,
    IIdentityService identity,
    IAccessTokenIssuer accessTokens,
    SecurityAudit audit) : ICommandHandler<RefreshSessionCommand, RefreshSessionResponse>
{
    private static readonly Error s_sessionInvalid =
        Error.Unauthorized("Auth.SessionInvalid", "Sesja wygasła lub jest nieprawidłowa. Zaloguj się ponownie.");

    public async Task<Result<RefreshSessionResponse>> HandleAsync(RefreshSessionCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        byte[]? tokenHash = command.RefreshToken is { } token ? OpaqueToken.Hash(token) : null;

        if (tokenHash is null)
        {
            return Result.Failure<RefreshSessionResponse>(s_sessionInvalid);
        }

        switch (await sessionStore.RotateAsync(tokenHash, ct))
        {
            case RotationResult.Rotated rotated:
                // The e-mail may have been verified since the last token, so read the current state.
                AccountSummary? account = await identity.GetSummaryAsync(rotated.Session.UserId, ct);

                if (account is null)
                {
                    // The account was deleted while its session lived on.
                    await sessionStore.RevokeAsync(rotated.Session.SessionId, ct);

                    return Result.Failure<RefreshSessionResponse>(s_sessionInvalid);
                }

                AccessToken accessToken = accessTokens.Issue(account.Id, rotated.Session.SessionId, account.EmailVerified);

                return new RefreshSessionResponse(new IssuedSession(
                    accessToken.Value,
                    accessToken.ExpiresAt,
                    account,
                    rotated.NewRefreshToken,
                    rotated.RemainingLifetime));

            case RotationResult.Reused reused:
                // The session is already revoked by the store. Record it against the session's own device.
                await audit.WriteAsync(
                    reused.Session.UserId,
                    SecurityEventType.RefreshTokenReuse,
                    SecurityEventOutcome.Failed,
                    command.Client with
                    {
                        DeviceId = reused.Session.Device.DeviceId,
                        DeviceType = reused.Session.Device.DeviceType,
                    },
                    ct);

                return Result.Failure<RefreshSessionResponse>(s_sessionInvalid);

            default:
                return Result.Failure<RefreshSessionResponse>(s_sessionInvalid);
        }
    }
}
