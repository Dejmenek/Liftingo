using FluentValidation.Results;

using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Domain.Account;

namespace Liftingo.Application.Features.Account.ForgotPassword;

/// <summary>
/// Sends a reset link to a registered address. Registered, unregistered and rate-limited addresses all get the same
/// answer (SC-008).
/// </summary>
public sealed class ForgotPasswordCommandHandler(
    ForgotPasswordCommandValidator validator,
    IIdentityService identity,
    IApplicationDbContext db,
    EmailTokenIssuer emailTokens,
    IEmailSender emails) : ICommandHandler<ForgotPasswordCommand>
{
    public async Task<Result> HandleAsync(ForgotPasswordCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidationResult validation = await validator.ValidateAsync(command, ct);

        if (!validation.IsValid)
        {
            return Result.Failure(validation.ToValidationError());
        }

        if (await identity.FindByEmailAsync(command.Email.Trim(), ct) is { } account
            && await emailTokens.IssueAsync(account.Id, EmailTokenPurpose.PasswordReset, ct) is { } token)
        {
            await db.SaveChangesAsync(ct);
            await emails.SendAsync(new PasswordResetEmail(account.Id, account.Email, token), ct);
        }

        return Result.Success();
    }
}
