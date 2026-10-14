using FluentValidation.Results;

using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.Login;

public sealed class LoginCommandHandler(
    LoginCommandValidator validator,
    IIdentityService identity,
    IApplicationDbContext db,
    ILoginThrottle throttle,
    SessionIssuer sessions,
    IEmailSender emails,
    SecurityAudit audit,
    TimeProvider timeProvider) : ICommandHandler<LoginCommand, LoginResponse>
{
    // One error for every kind of wrong credential, so the answer never says whether the address is registered.
    private static readonly Error s_invalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Nieprawidłowy adres e-mail lub hasło.");

    public async Task<Result<LoginResponse>> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(command, ct);

        if (!validation.IsValid)
        {
            return Result.Failure<LoginResponse>(validation.ToValidationError());
        }

        string email = command.Email.Trim();

        AccountSummary? account = await identity.FindByEmailAsync(email, ct);

        return account is null
            ? await HandleUnknownEmailAsync(email, command.Password, command.Client, ct)
            : await HandleKnownAccountAsync(account, command.Password, command.Client, ct);
    }

    private async Task<Result<LoginResponse>> HandleKnownAccountAsync(
        AccountSummary account,
        string password,
        ClientInfo client,
        CancellationToken ct)
    {
        PasswordCheckResult check = await identity.CheckPasswordAsync(account.Id, password, ct);

        switch (check)
        {
            case PasswordCheckResult.LockedOut lockedOut:
                // Refused before the password was checked: nothing changed, so there is nothing to save.
                await audit.WriteAsync(account.Id, SecurityEventType.Login, SecurityEventOutcome.LockedOut, client, ct);

                return Result.Failure<LoginResponse>(new LockedOutError(lockedOut.RetryAfter));

            case PasswordCheckResult.LockoutTriggered triggered:
                // Persists LockoutEnd and the reset counter.
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync(account.Id, SecurityEventType.Login, SecurityEventOutcome.Failed, client, ct);
                await audit.WriteAsync(account.Id, SecurityEventType.LockoutTriggered, SecurityEventOutcome.LockedOut, client, ct);

                return Result.Failure<LoginResponse>(new LockedOutError(triggered.RetryAfter));

            case PasswordCheckResult.Failed:
                // Persists the higher failed-attempt count.
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync(account.Id, SecurityEventType.Login, SecurityEventOutcome.Failed, client, ct);

                return Result.Failure<LoginResponse>(s_invalidCredentials);

            default:
                // Succeeded. The same commit persists the failed-attempt counter reset made by the password check.
                // A device the account has not used before gets a security notice once the session exists.
                bool isNewDevice = await sessions.StageDeviceAsync(account.Id, client, SignInKind.SignIn, ct);

                await db.SaveChangesAsync(ct);

                IssuedSession session = await sessions.IssueAsync(account.Id, client, ct);
                await audit.WriteAsync(account.Id, SecurityEventType.Login, SecurityEventOutcome.Succeeded, client, ct);

                if (isNewDevice)
                {
                    await emails.SendAsync(
                        new NewDeviceEmail(account.Id, account.Email, timeProvider.GetUtcNow(), client.DeviceType),
                        ct);
                }

                return new LoginResponse(session);
        }
    }

    private async Task<Result<LoginResponse>> HandleUnknownEmailAsync(
        string email,
        string password,
        ClientInfo client,
        CancellationToken ct)
    {
        // Same rules as a real account, so a lockout answer does not reveal that the address has no account.
        if (await throttle.GetLockoutEndAsync(email, ct) is { } lockedUntil)
        {
            await audit.WriteAsync(null, SecurityEventType.Login, SecurityEventOutcome.LockedOut, client, ct);

            return Result.Failure<LoginResponse>(new LockedOutError(lockedUntil));
        }

        // Spend the time a real password check would take.
        await identity.VerifyDummyPasswordAsync(password);

        PasswordCheckResult.LockoutTriggered? triggered = await throttle.RegisterFailureAsync(email, ct);

        await audit.WriteAsync(null, SecurityEventType.Login, SecurityEventOutcome.Failed, client, ct);

        if (triggered is null)
        {
            return Result.Failure<LoginResponse>(s_invalidCredentials);
        }

        await audit.WriteAsync(null, SecurityEventType.LockoutTriggered, SecurityEventOutcome.LockedOut, client, ct);

        return Result.Failure<LoginResponse>(new LockedOutError(triggered.RetryAfter));
    }
}