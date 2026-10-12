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

public sealed record ForgotPasswordRequest(string Email);

// No response body: registered, unregistered and rate-limited addresses all get the same empty 202 (SC-008).

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator() => RuleFor(r => r.Email).MustBeValidEmail();
}

public sealed class ForgotPassword : IEndpoint
{
    private static readonly ForgotPasswordValidator s_validator = new();

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(RouteConsts.Auth.ForgotPassword, Handle)
            .AllowAnonymous()
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<IResult> Handle(
        ForgotPasswordRequest request,
        IIdentityService identity,
        IApplicationDbContext db,
        EmailTokenIssuer emailTokens,
        AccountEmails emails,
        CancellationToken ct)
    {
        ValidationResult validation = await s_validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
        {
            return Result.Failure(validation.ToValidationError()).ToProblem();
        }

        if (await identity.FindByEmailAsync(request.Email.Trim(), ct) is { } account
            && await emailTokens.IssueAsync(account.Id, EmailTokenPurpose.PasswordReset, ct) is { } token)
        {
            await db.SaveChangesAsync(ct);
            await emails.SendPasswordResetAsync(account.Id, account.Email, token, ct);
        }

        return Results.Accepted();
    }
}
