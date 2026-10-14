using Microsoft.Extensions.Options;

namespace Liftingo.Application.Common.Configuration;

public sealed class AppOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        if (options.FrontendBaseUrl is not { IsAbsoluteUri: true } uri || uri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail(
                $"'{AppOptions.SectionName}:{nameof(AppOptions.FrontendBaseUrl)}' must be an absolute https URL.");
        }

        return ValidateOptionsResult.Success;
    }
}