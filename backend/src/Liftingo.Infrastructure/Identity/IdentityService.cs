using System.Security.Cryptography;

using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Liftingo.Infrastructure.Identity;

/// <summary>
/// <see cref="IIdentityService"/> over <see cref="UserManager{TUser}"/>. The user store does not save
/// (see <see cref="LiftingoUserStore"/>), so every method only stages changes.
/// </summary>
public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IOptions<IdentityOptions> identityOptions) : IIdentityService
{
    // Hashed once per process, with the same hasher and settings as real passwords.
    private static string? s_dummyPasswordHash;

    public async Task<Result<Guid>> CreateUserAsync(
        string email,
        string? password,
        bool emailConfirmed,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = emailConfirmed,
            CreatedAt = now,
        };

        IdentityResult result = password is null
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return Result.Failure<Guid>(ToError(result));
        }

        return user.Id;
    }

    public async Task<AccountSummary?> FindByEmailAsync(string email, CancellationToken ct)
    {
        ApplicationUser? user = await userManager.FindByEmailAsync(email);

        return user is null ? null : await ToSummaryAsync(user);
    }

    public async Task<AccountSummary?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken ct)
    {
        ApplicationUser? user = await userManager.FindByLoginAsync(loginProvider, providerKey);

        return user is null ? null : await ToSummaryAsync(user);
    }

    public async Task<AccountSummary?> GetSummaryAsync(Guid userId, CancellationToken ct)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(userId.ToString());

        return user is null ? null : await ToSummaryAsync(user);
    }

    public async Task<PasswordCheckResult> CheckPasswordAsync(Guid userId, string password, CancellationToken ct)
    {
        ApplicationUser user = await GetUserAsync(userId);

        if (await userManager.IsLockedOutAsync(user))
        {
            return new PasswordCheckResult.LockedOut(await GetRequiredLockoutEndAsync(user));
        }

        if (user.PasswordHash is null)
        {
            // A provider-only account: spend the same time as a real check, then count the failure.
            await VerifyDummyPasswordAsync(password);
        }
        else if (await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.ResetAccessFailedCountAsync(user);

            return new PasswordCheckResult.Succeeded();
        }

        await userManager.AccessFailedAsync(user);

        return await userManager.IsLockedOutAsync(user)
            ? new PasswordCheckResult.LockoutTriggered(await GetRequiredLockoutEndAsync(user))
            : new PasswordCheckResult.Failed();
    }

    public async Task<DateTimeOffset?> GetLockoutEndAsync(Guid userId, CancellationToken ct)
    {
        ApplicationUser user = await GetUserAsync(userId);

        return await userManager.IsLockedOutAsync(user)
            ? await GetRequiredLockoutEndAsync(user)
            : null;
    }

    public Task VerifyDummyPasswordAsync(string password)
    {
        string hash = LazyInitializer.EnsureInitialized(
            ref s_dummyPasswordHash,
            () => passwordHasher.HashPassword(new ApplicationUser(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))));

        _ = passwordHasher.VerifyHashedPassword(new ApplicationUser(), hash, password);

        return Task.CompletedTask;
    }

    public async Task AddLoginAsync(
        Guid userId,
        string loginProvider,
        string providerKey,
        string? displayName,
        CancellationToken ct)
    {
        ApplicationUser user = await GetUserAsync(userId);

        IdentityResult result = await userManager.AddLoginAsync(user, new UserLoginInfo(loginProvider, providerKey, displayName));

        ThrowIfFailed(result, nameof(AddLoginAsync));
    }

    public async Task ConfirmEmailAsync(Guid userId, CancellationToken ct)
    {
        ApplicationUser user = await GetUserAsync(userId);

        user.EmailConfirmed = true;

        ThrowIfFailed(await userManager.UpdateAsync(user), nameof(ConfirmEmailAsync));
    }

    public async Task RemovePasswordAsync(Guid userId, CancellationToken ct)
    {
        ApplicationUser user = await GetUserAsync(userId);

        ThrowIfFailed(await userManager.RemovePasswordAsync(user), nameof(RemovePasswordAsync));
    }

    public async Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct)
    {
        ApplicationUser user = await GetUserAsync(userId);

        // UserManager only offers a reset that takes a token, so it gets one it generates for itself.
        // The reset validates the new password before anything changes.
        string token = await userManager.GeneratePasswordResetTokenAsync(user);

        IdentityResult result = await userManager.ResetPasswordAsync(user, token, newPassword);

        if (!result.Succeeded)
        {
            return Result.Failure(ToError(result));
        }

        ThrowIfFailed(await userManager.SetLockoutEndDateAsync(user, null), nameof(ResetPasswordAsync));
        ThrowIfFailed(await userManager.ResetAccessFailedCountAsync(user), nameof(ResetPasswordAsync));

        return Result.Success();
    }

    private async Task<ApplicationUser> GetUserAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
        ?? throw new InvalidOperationException($"User {userId} does not exist.");

    private async Task<DateTimeOffset> GetRequiredLockoutEndAsync(ApplicationUser user) =>
        await userManager.GetLockoutEndDateAsync(user)
        ?? throw new InvalidOperationException($"User {user.Id} is locked out but has no lockout end.");

    private async Task<AccountSummary> ToSummaryAsync(ApplicationUser user)
    {
        IList<UserLoginInfo> logins = await userManager.GetLoginsAsync(user);

        return new AccountSummary(
            user.Id,
            user.Email!,
            user.EmailConfirmed,
            user.PasswordHash is not null,
            [.. logins.Select(login => login.LoginProvider)],
            user.CreatedAt);
    }

    private Error ToError(IdentityResult result)
    {
        List<Error> validation = [];

        foreach (string code in result.Errors.Select(error => error.Code))
        {
            switch (code)
            {
                case nameof(IdentityErrorDescriber.PasswordTooShort):
                    validation.Add(Error.Validation(
                        "Validation.PasswordTooShort",
                        $"Hasło musi mieć co najmniej {identityOptions.Value.Password.RequiredLength} znaków."));
                    break;
                case nameof(IdentityErrorDescriber.PasswordRequiresDigit):
                    validation.Add(Error.Validation(
                        "Validation.PasswordRequiresDigit",
                        "Hasło musi zawierać co najmniej jedną cyfrę."));
                    break;
                case nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric):
                    validation.Add(Error.Validation(
                        "Validation.PasswordRequiresSpecial",
                        "Hasło musi zawierać co najmniej jeden znak specjalny (nie literę ani cyfrę)."));
                    break;
                case nameof(IdentityErrorDescriber.DuplicateEmail):
                case nameof(IdentityErrorDescriber.DuplicateUserName):
                    return Error.Conflict("Auth.EmailTaken", "Konto z tym adresem e-mail już istnieje.");
                default:
                    // Identity's own messages can name the e-mail, so only the code is kept.
                    throw new InvalidOperationException($"Unexpected Identity error '{code}'.");
            }
        }

        return new ValidationError(validation);
    }

    private static void ThrowIfFailed(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            string codes = string.Join(", ", result.Errors.Select(error => error.Code));

            throw new InvalidOperationException($"{operation} failed: {codes}.");
        }
    }
}
