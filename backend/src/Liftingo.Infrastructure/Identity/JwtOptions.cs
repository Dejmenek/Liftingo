using Microsoft.Extensions.Options;

namespace Liftingo.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // HS256 needs at least a 256-bit key.
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    // Base64. Comes from user secrets or the environment, never from appsettings.json.
    public string SigningKey { get; init; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; init; }
}

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add($"'{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)}' must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"'{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)}' must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add($"'{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}' must be configured.");
        }
        else if (!IsBase64KeyOfSufficientLength(options.SigningKey))
        {
            failures.Add(
                $"'{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}' must be Base64 that decodes to at least " +
                $"{JwtOptions.MinimumSigningKeyBytes} bytes.");
        }

        if (options.AccessTokenLifetime <= TimeSpan.Zero)
        {
            failures.Add($"'{JwtOptions.SectionName}:{nameof(JwtOptions.AccessTokenLifetime)}' must be greater than zero.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static bool IsBase64KeyOfSufficientLength(string value)
    {
        // Base64 never decodes to more bytes than its length, so this buffer is always large enough.
        byte[] buffer = new byte[value.Length];

        return Convert.TryFromBase64String(value, buffer, out int bytesWritten) &&
            bytesWritten >= JwtOptions.MinimumSigningKeyBytes;
    }
}