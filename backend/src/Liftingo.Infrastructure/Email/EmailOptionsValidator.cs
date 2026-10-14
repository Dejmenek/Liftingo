using Microsoft.Extensions.Options;

namespace Liftingo.Infrastructure.Email;

public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    private const string DisplayTimeZonePath = EmailOptions.SectionName + ":" + nameof(EmailOptions.DisplayTimeZone);

    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.DisplayTimeZone))
        {
            return ValidateOptionsResult.Fail($"'{DisplayTimeZonePath}' must be configured.");
        }

        // No fallback to UTC: an unknown zone has to show up at deployment, not as wrong times in e-mails.
        return TimeZoneInfo.TryFindSystemTimeZoneById(options.DisplayTimeZone, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"'{DisplayTimeZonePath}' is '{options.DisplayTimeZone}', which is not a time zone known on this host.");
    }
}