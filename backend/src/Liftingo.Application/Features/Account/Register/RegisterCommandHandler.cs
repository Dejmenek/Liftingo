using FluentValidation.Results;

using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Exceptions;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.Register;

public sealed class RegisterCommandHandler(
    RegisterCommandValidator validator,
    IIdentityService identity,
    IApplicationDbContext db,
    ConsentRecorder consents,
    SessionIssuer sessions,
    EmailTokenIssuer emailTokens,
    IEmailSender emails,
    TimeProvider timeProvider)
{
    private static readonly Error s_emailTaken =
        Error.Conflict("Auth.EmailTaken", "Konto z tym adresem e-mail już istnieje.");

    public async Task<Result<RegisterResponse>> HandleAsync(RegisterCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidationResult validation = await validator.ValidateAsync(command, ct);

        if (!validation.IsValid)
        {
            return Result.Failure<RegisterResponse>(validation.ToValidationError());
        }

        // Stored and looked up trimmed (FR-005); the validator trims only to check.
        string email = command.Email.Trim();

        if (await identity.FindByEmailAsync(email, ct) is not null)
        {
            return Result.Failure<RegisterResponse>(s_emailTaken);
        }

        // Nothing below saves until the single SaveChangesAsync, so a failure leaves no half-created account (SC-007).
        Result<Guid> created = await identity.CreateUserAsync(
            email,
            command.Password,
            emailConfirmed: false,
            timeProvider.GetUtcNow(),
            ct);

        if (created.IsFailure)
        {
            return Result.Failure<RegisterResponse>(created.Error);
        }

        Guid userId = created.Value;

        consents.Stage(userId, command.AcceptTermsAndAge, command.AcceptHealthData, command.AcceptMarketing);
        await sessions.StageDeviceAsync(userId, command.Client, SignInKind.Registration, ct);
        string? verificationToken = await emailTokens.IssueAsync(userId, EmailTokenPurpose.EmailVerification, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // Two registrations of one address raced past the FindByEmailAsync check above. That is an unexpected
            // condition the unique index resolves, and the handler turns it into a Result (constitution II).
            return Result.Failure<RegisterResponse>(s_emailTaken);
        }

        // SQL is committed; now Redis (session), then the e-mail (R4, FR-029).
        IssuedSession session = await sessions.IssueAsync(userId, command.Client, ct);

        if (verificationToken is not null)
        {
            await emails.SendAsync(new VerificationAndWelcomeEmail(userId, email, verificationToken), ct);
        }

        return new RegisterResponse(session);
    }
}
