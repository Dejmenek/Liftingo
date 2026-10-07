using Microsoft.Extensions.Options;

namespace Liftingo.Infrastructure.Identity;

public sealed class IdentitySecurityOptions
{
    public const string SectionName = "Identity";

    public PasswordPolicyOptions Password { get; init; } = new();

    public LockoutPolicyOptions Lockout { get; init; } = new();
}

public sealed class PasswordPolicyOptions
{
    public int RequiredLength { get; init; }

    public bool RequireDigit { get; init; }

    public bool RequireSpecialCharacter { get; init; }
}

public sealed class LockoutPolicyOptions
{
    public int MaxFailedAccessAttempts { get; init; }

    public TimeSpan Duration { get; init; }
}

public sealed class IdentitySecurityOptionsValidator : IValidateOptions<IdentitySecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentitySecurityOptions options)
    {
        List<string> failures = [];

        if (options.Password.RequiredLength < 1)
        {
            failures.Add($"'{IdentitySecurityOptions.SectionName}:Password:{nameof(PasswordPolicyOptions.RequiredLength)}' must be at least 1.");
        }

        if (options.Lockout.MaxFailedAccessAttempts < 1)
        {
            failures.Add($"'{IdentitySecurityOptions.SectionName}:Lockout:{nameof(LockoutPolicyOptions.MaxFailedAccessAttempts)}' must be at least 1.");
        }

        if (options.Lockout.Duration <= TimeSpan.Zero)
        {
            failures.Add($"'{IdentitySecurityOptions.SectionName}:Lockout:{nameof(LockoutPolicyOptions.Duration)}' must be greater than zero.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}