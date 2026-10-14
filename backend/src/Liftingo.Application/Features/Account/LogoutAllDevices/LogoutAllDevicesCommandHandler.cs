using Liftingo.Application.Common;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;
using Microsoft.EntityFrameworkCore;

namespace Liftingo.Application.Features.Account.LogoutAllDevices;

/// <summary>Ends every session of the signed-in user, the current one included, and forgets their devices.</summary>
public sealed class LogoutAllDevicesCommandHandler(
    IUserContext userContext,
    IApplicationDbContext db,
    ISessionStore sessionStore,
    SecurityAudit audit) : ICommandHandler<LogoutAllDevicesCommand>
{
    public async Task<Result> HandleAsync(LogoutAllDevicesCommand command, CancellationToken ct)
    {
        Guid userId = userContext.UserId;

        // Forgetting the devices is the only SQL write here, so it needs no transaction. The next sign-in from any
        // device counts as new and sends the new-device e-mail.
        await db.KnownDevices.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);

        await sessionStore.RevokeAllAsync(userId, ct);

        await audit.WriteAsync(userId, SecurityEventType.LogoutAllDevices, SecurityEventOutcome.Succeeded, command.Client, ct);

        return Result.Success();
    }
}