using FluentValidation;
using FluentValidation.Results;

using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

public sealed record LoginRequest(string Email, string Password);

// The response is the shared AuthSessionResponse (Shared/AuthSessionResponse.cs).

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        // Only presence is checked: a malformed address gets the same 401 as any other wrong credential.
        RuleFor(r => r.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email))
            .WithErrorCode("Validation.EmailRequired")
            .WithMessage("Podaj adres e-mail.");

        RuleFor(r => r.Password)
            .Must(password => !string.IsNullOrEmpty(password))
            .WithErrorCode("Validation.PasswordRequired")
            .WithMessage("Podaj hasło.");
    }
}

public sealed class Login : IEndpoint
{
    private static readonly LoginValidator s_validator = new();

    // One error for every kind of wrong credential, so the answer never says whether the address is registered.
    private static readonly Error s_invalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Nieprawidłowy adres e-mail lub hasło.");

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.Login, Handle)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> Handle(
        LoginRequest request,
        HttpContext http,
        IIdentityService identity,
        IApplicationDbContext db,
        ILoginThrottle throttle,
        SessionIssuer sessions,
        IEmailSender emails,
        TimeProvider timeProvider,
        SecurityAudit audit,
        CancellationToken ct)
    {
        ValidationResult validation = await s_validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
        {
            return Result.Failure(validation.ToValidationError()).ToProblem();
        }

        string email = request.Email.Trim();
        ClientInfo client = ClientInfo.From(http);

        AccountSummary? account = await identity.FindByEmailAsync(email, ct);

        return account is null
            ? await HandleUnknownEmailAsync(email, request.Password, identity, throttle, audit, client, ct)
            : await HandleKnownAccountAsync(http, account, request.Password, identity, db, sessions, emails, timeProvider, audit, client, ct);
    }

    private static async Task<IResult> HandleKnownAccountAsync(
        HttpContext http,
        AccountSummary account,
        string password,
        IIdentityService identity,
        IApplicationDbContext db,
        SessionIssuer sessions,
        IEmailSender emails,
        TimeProvider timeProvider,
        SecurityAudit audit,
        ClientInfo client,
        CancellationToken ct)
    {
        PasswordCheckResult check = await identity.CheckPasswordAsync(account.Id, password, ct);

        switch (check)
        {
            case PasswordCheckResult.LockedOut lockedOut:
                // Refused before the password was checked: nothing changed, so there is nothing to save.
                await audit.WriteAsync(account.Id, SecurityEventType.Login, SecurityEventOutcome.LockedOut, client, ct);

                return Result.Failure(new LockedOutError(lockedOut.RetryAfter)).ToProblem();

            case PasswordCheckResult.LockoutTriggered triggered:
                // Persists LockoutEnd and the reset counter.
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync(account.Id, SecurityEventType.Login, SecurityEventOutcome.Failed, client, ct);
                await audit.WriteAsync(account.Id, SecurityEventType.LockoutTriggered, SecurityEventOutcome.LockedOut, client, ct);

                return Result.Failure(new LockedOutError(triggered.RetryAfter)).ToProblem();

            case PasswordCheckResult.Failed:
                // Persists the higher failed-attempt count.
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync(account.Id, SecurityEventType.Login, SecurityEventOutcome.Failed, client, ct);

                return Result.Failure(s_invalidCredentials).ToProblem();

            default:
                // Succeeded. The same commit persists the failed-attempt counter reset made by the password check.
                // A device the account has not used before gets a security notice once the session exists (FR-026).
                bool isNewDevice = await sessions.StageDeviceAsync(account.Id, client, SignInKind.SignIn, ct);

                await db.SaveChangesAsync(ct);

                AuthSessionResponse response = await sessions.IssueAsync(http, account.Id, client, ct);
                await audit.WriteAsync(account.Id, SecurityEventType.Login, SecurityEventOutcome.Succeeded, client, ct);

                if (isNewDevice)
                {
                    await emails.SendAsync(new NewDeviceEmail(account.Id, account.Email, timeProvider.GetUtcNow(), client.DeviceType), ct);
                }

                return Results.Ok(response);
        }
    }

    private static async Task<IResult> HandleUnknownEmailAsync(
        string email,
        string password,
        IIdentityService identity,
        ILoginThrottle throttle,
        SecurityAudit audit,
        ClientInfo client,
        CancellationToken ct)
    {
        // Same rules as a real account, so a lockout answer does not reveal that the address has no account (R8).
        if (await throttle.GetLockoutEndAsync(email, ct) is { } lockedUntil)
        {
            await audit.WriteAsync(null, SecurityEventType.Login, SecurityEventOutcome.LockedOut, client, ct);

            return Result.Failure(new LockedOutError(lockedUntil)).ToProblem();
        }

        // Spend the time a real password check would take.
        await identity.VerifyDummyPasswordAsync(password);

        PasswordCheckResult.LockoutTriggered? triggered = await throttle.RegisterFailureAsync(email, ct);

        await audit.WriteAsync(null, SecurityEventType.Login, SecurityEventOutcome.Failed, client, ct);

        if (triggered is null)
        {
            return Result.Failure(s_invalidCredentials).ToProblem();
        }

        await audit.WriteAsync(null, SecurityEventType.LockoutTriggered, SecurityEventOutcome.LockedOut, client, ct);

        return Result.Failure(new LockedOutError(triggered.RetryAfter)).ToProblem();
    }
}
