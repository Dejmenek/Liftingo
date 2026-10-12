using FluentValidation;
using FluentValidation.Results;

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

public sealed record VerifyEmailRequest(string Token);

/// <param name="Status"><c>verified</c> or <c>alreadyVerified</c>.</param>
public sealed record VerifyEmailResponse(string Status);

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailValidator() =>
        RuleFor(r => r.Token)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .WithErrorCode("Validation.TokenRequired")
            .WithMessage("Brakuje tokenu weryfikacyjnego.");
}

public sealed class VerifyEmail : IEndpoint
{
    public const string Verified = "verified";
    public const string AlreadyVerified = "alreadyVerified";

    private static readonly VerifyEmailValidator s_validator = new();

    private static readonly Error s_linkInvalid = Error.Validation(
        "Auth.VerificationLinkInvalid",
        "Link weryfikacyjny jest nieprawidłowy, wygasł albo został już zastąpiony nowszym. Poproś o nowy link.");

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.VerifyEmail, Handle)
            .AllowAnonymous()
            .Produces<VerifyEmailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

    // Anonymous on purpose: the token identifies the account, so a signed-in different user is ignored.
    private static async Task<IResult> Handle(
        VerifyEmailRequest request,
        IIdentityService identity,
        IApplicationDbContext db,
        EmailTokenIssuer emailTokens,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        ValidationResult validation = await s_validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
        {
            return Result.Failure(validation.ToValidationError()).ToProblem();
        }

        EmailToken? token = await emailTokens.FindAsync(request.Token, EmailTokenPurpose.EmailVerification, ct);

        if (token is null || await identity.GetSummaryAsync(token.UserId, ct) is not { } account)
        {
            return Result.Failure(s_linkInvalid).ToProblem();
        }

        // Whatever the token's state: a verified account (provider sign-in, password reset) reports success (FR-017).
        if (account.EmailVerified)
        {
            return Results.Ok(new VerifyEmailResponse(AlreadyVerified));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (!token.IsUsable(now))
        {
            return Result.Failure(s_linkInvalid).ToProblem();
        }

        token.Consume(now);
        await identity.ConfirmEmailAsync(token.UserId, ct);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new VerifyEmailResponse(Verified));
    }
}
