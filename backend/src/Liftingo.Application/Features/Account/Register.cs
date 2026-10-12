using FluentValidation;
using FluentValidation.Results;

using Liftingo.Application.Common.Exceptions;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

public sealed record RegisterRequest(
    string Email,
    string Password,
    bool AcceptTermsAndAge,
    bool AcceptHealthData,
    bool AcceptMarketing);

// The response is the shared AuthSessionResponse (Shared/AuthSessionResponse.cs).

public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public const int PasswordMaxLength = 128;

    public RegisterValidator()
    {
        // The other password rules (length, digit, special character) belong to Identity and are reported by
        // IIdentityService.CreateUserAsync with their own codes.
        RuleFor(r => r.Email).MustBeValidEmail();

        RuleFor(r => r.Password)
            .Must(password => !string.IsNullOrEmpty(password))
            .WithErrorCode("Validation.PasswordRequired")
            .WithMessage("Podaj hasło.")
            .Must(password => password is null || password.Length <= PasswordMaxLength)
            .WithErrorCode("Validation.PasswordTooLong")
            .WithMessage($"Hasło może mieć najwyżej {PasswordMaxLength} znaków.");

        RuleFor(r => r.AcceptTermsAndAge).MustAcceptTermsAndAge();
        RuleFor(r => r.AcceptHealthData).MustAcceptHealthData();
    }
}

public sealed class Register : IEndpoint
{
    private static readonly RegisterValidator s_validator = new();

    private static readonly Error s_emailTaken =
        Error.Conflict("Auth.EmailTaken", "Konto z tym adresem e-mail już istnieje.");

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.Register, Handle)
            .AllowAnonymous()
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> Handle(
        RegisterRequest request,
        HttpContext http,
        IIdentityService identity,
        IApplicationDbContext db,
        ConsentRecorder consents,
        SessionIssuer sessions,
        EmailTokenIssuer emailTokens,
        AccountEmails emails,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        ValidationResult validation = await s_validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
        {
            return Result.Failure(validation.ToValidationError()).ToProblem();
        }

        // Stored and looked up trimmed (FR-005); the validator trims only to check.
        string email = request.Email.Trim();

        if (await identity.FindByEmailAsync(email, ct) is not null)
        {
            return Result.Failure(s_emailTaken).ToProblem();
        }

        // Nothing below saves until the single SaveChangesAsync, so a failure leaves no half-created account (SC-007).
        Result<Guid> created = await identity.CreateUserAsync(
            email,
            request.Password,
            emailConfirmed: false,
            timeProvider.GetUtcNow(),
            ct);

        if (created.IsFailure)
        {
            return created.ToProblem();
        }

        Guid userId = created.Value;
        ClientInfo client = ClientInfo.From(http);

        consents.Stage(userId, request.AcceptTermsAndAge, request.AcceptHealthData, request.AcceptMarketing);
        await sessions.StageDeviceAsync(userId, client, SignInKind.Registration, ct);
        string? verificationToken = await emailTokens.IssueAsync(userId, EmailTokenPurpose.EmailVerification, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // Two registrations of one address raced past the FindByEmailAsync check above. That is an unexpected
            // condition the unique index resolves, and the handler turns it into a Result (constitution II).
            return Result.Failure(s_emailTaken).ToProblem();
        }

        // SQL is committed; now Redis (session), then the e-mail (R4).
        AuthSessionResponse response = await sessions.IssueAsync(http, userId, client, ct);

        if (verificationToken is not null)
        {
            await emails.SendVerificationAndWelcomeAsync(userId, email, verificationToken, ct);
        }

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }
}
