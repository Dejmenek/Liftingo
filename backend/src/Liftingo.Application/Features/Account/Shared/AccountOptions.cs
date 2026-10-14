using Liftingo.Domain.Account;

using Microsoft.Extensions.Options;

namespace Liftingo.Application.Features.Account.Shared;

public sealed class SessionsOptions
{
    public const string SectionName = "Sessions";

    // Sliding lifetime: a session that is not renewed within this time is gone.
    public TimeSpan InactivityTimeout { get; init; }

    // Hard cap measured from the sign-in, however often the session is renewed.
    public TimeSpan AbsoluteLifetime { get; init; }
}

public sealed class KnownDevicesOptions
{
    public const string SectionName = "KnownDevices";

    public TimeSpan Retention { get; init; }
}

public sealed class EmailTokensOptions
{
    public const string SectionName = "EmailTokens";

    public TimeSpan VerificationLifetime { get; init; }

    public TimeSpan PasswordResetLifetime { get; init; }

    public TimeSpan ProviderLinkLifetime { get; init; }

    public int MaxPerAddressPerHour { get; init; }

    public TimeSpan RetentionAfterUnusable { get; init; }
}

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    public TimeSpan Retention { get; init; }
}

public sealed class LegalOptions
{
    public const string SectionName = "Legal";

    public string DocumentsVersion { get; init; } = string.Empty;
}

/// <summary>
/// The failed-login lockout rules that <c>ILoginThrottle</c> applies to e-mails without an account.
/// They are the values Identity uses for real accounts: Infrastructure fills them from <c>Identity:Lockout</c>,
/// so both paths always agree.
/// </summary>
public sealed class LoginLockoutOptions
{
    public int MaxFailedAccessAttempts { get; init; }

    public TimeSpan Duration { get; init; }
}

internal static class OptionsValidation
{
    public static void RequirePositive(string section, string property, TimeSpan value, List<string> failures)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"'{section}:{property}' must be greater than zero.");
        }
    }

    public static ValidateOptionsResult ToResult(List<string> failures) =>
        failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
}

public sealed class SessionsOptionsValidator : IValidateOptions<SessionsOptions>
{
    public ValidateOptionsResult Validate(string? name, SessionsOptions options)
    {
        List<string> failures = [];

        OptionsValidation.RequirePositive(
            SessionsOptions.SectionName, nameof(SessionsOptions.InactivityTimeout), options.InactivityTimeout, failures);
        OptionsValidation.RequirePositive(
            SessionsOptions.SectionName, nameof(SessionsOptions.AbsoluteLifetime), options.AbsoluteLifetime, failures);

        if (options.AbsoluteLifetime < options.InactivityTimeout)
        {
            failures.Add(
                $"'{SessionsOptions.SectionName}:{nameof(SessionsOptions.AbsoluteLifetime)}' must not be shorter than " +
                $"'{SessionsOptions.SectionName}:{nameof(SessionsOptions.InactivityTimeout)}'.");
        }

        return OptionsValidation.ToResult(failures);
    }
}

public sealed class KnownDevicesOptionsValidator : IValidateOptions<KnownDevicesOptions>
{
    public ValidateOptionsResult Validate(string? name, KnownDevicesOptions options)
    {
        List<string> failures = [];

        OptionsValidation.RequirePositive(
            KnownDevicesOptions.SectionName, nameof(KnownDevicesOptions.Retention), options.Retention, failures);

        return OptionsValidation.ToResult(failures);
    }
}

public sealed class EmailTokensOptionsValidator : IValidateOptions<EmailTokensOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailTokensOptions options)
    {
        List<string> failures = [];

        OptionsValidation.RequirePositive(
            EmailTokensOptions.SectionName, nameof(EmailTokensOptions.VerificationLifetime), options.VerificationLifetime, failures);
        OptionsValidation.RequirePositive(
            EmailTokensOptions.SectionName, nameof(EmailTokensOptions.PasswordResetLifetime), options.PasswordResetLifetime, failures);
        OptionsValidation.RequirePositive(
            EmailTokensOptions.SectionName, nameof(EmailTokensOptions.ProviderLinkLifetime), options.ProviderLinkLifetime, failures);
        OptionsValidation.RequirePositive(
            EmailTokensOptions.SectionName, nameof(EmailTokensOptions.RetentionAfterUnusable), options.RetentionAfterUnusable, failures);

        if (options.MaxPerAddressPerHour < 1)
        {
            failures.Add($"'{EmailTokensOptions.SectionName}:{nameof(EmailTokensOptions.MaxPerAddressPerHour)}' must be at least 1.");
        }

        return OptionsValidation.ToResult(failures);
    }
}

public sealed class AuditOptionsValidator : IValidateOptions<AuditOptions>
{
    public ValidateOptionsResult Validate(string? name, AuditOptions options)
    {
        List<string> failures = [];

        OptionsValidation.RequirePositive(
            AuditOptions.SectionName, nameof(AuditOptions.Retention), options.Retention, failures);

        return OptionsValidation.ToResult(failures);
    }
}

public sealed class LegalOptionsValidator : IValidateOptions<LegalOptions>
{
    public ValidateOptionsResult Validate(string? name, LegalOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.DocumentsVersion))
        {
            failures.Add($"'{LegalOptions.SectionName}:{nameof(LegalOptions.DocumentsVersion)}' must be configured.");
        }
        else if (options.DocumentsVersion.Length > UserConsent.DocumentVersionMaxLength)
        {
            failures.Add(
                $"'{LegalOptions.SectionName}:{nameof(LegalOptions.DocumentsVersion)}' must be at most " +
                $"{UserConsent.DocumentVersionMaxLength} characters.");
        }

        return OptionsValidation.ToResult(failures);
    }
}
