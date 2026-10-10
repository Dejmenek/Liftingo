using Microsoft.Extensions.Options;

namespace Liftingo.Api.RequestTimeouts;

public sealed class RequestTimeoutsOptions
{
    public const string SectionName = "RequestTimeouts";

    public TimeSpan DefaultTimeout { get; init; }

    public Dictionary<string, TimeSpan> Policies { get; init; } = [];
}

public sealed class RequestTimeoutsOptionsValidator : IValidateOptions<RequestTimeoutsOptions>
{
    public ValidateOptionsResult Validate(string? name, RequestTimeoutsOptions options)
    {
        List<string> failures = [];

        if (options.DefaultTimeout <= TimeSpan.Zero)
        {
            failures.Add($"'{RequestTimeoutsOptions.SectionName}:{nameof(RequestTimeoutsOptions.DefaultTimeout)}' must be greater than zero.");
        }

        foreach ((string policyName, TimeSpan timeout) in options.Policies)
        {
            string path = $"{RequestTimeoutsOptions.SectionName}:{nameof(RequestTimeoutsOptions.Policies)}:{policyName}";

            if (string.IsNullOrWhiteSpace(policyName))
            {
                failures.Add($"'{RequestTimeoutsOptions.SectionName}:{nameof(RequestTimeoutsOptions.Policies)}' must not contain a blank policy name.");
            }
            else if (timeout <= TimeSpan.Zero)
            {
                failures.Add($"'{path}' must be greater than zero.");
            }
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}