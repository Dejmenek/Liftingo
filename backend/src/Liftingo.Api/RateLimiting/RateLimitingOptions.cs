using Microsoft.Extensions.Options;

namespace Liftingo.Api.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public TokenBucketPolicyOptions Authenticated { get; init; } = new();

    public TokenBucketPolicyOptions Anonymous { get; init; } = new();
}

public sealed class TokenBucketPolicyOptions
{
    public int TokenLimit { get; init; }

    public int TokensPerPeriod { get; init; }

    public TimeSpan ReplenishmentPeriod { get; init; }
}

public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        List<string> failures = [];

        ValidatePolicy(nameof(RateLimitingOptions.Authenticated), options.Authenticated, failures);
        ValidatePolicy(nameof(RateLimitingOptions.Anonymous), options.Anonymous, failures);

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static void ValidatePolicy(string policyName, TokenBucketPolicyOptions policy, List<string> failures)
    {
        string prefix = $"'{RateLimitingOptions.SectionName}:{policyName}";

        if (policy.TokenLimit < 1)
        {
            failures.Add($"{prefix}:{nameof(TokenBucketPolicyOptions.TokenLimit)}' must be at least 1.");
        }

        if (policy.TokensPerPeriod < 1)
        {
            failures.Add($"{prefix}:{nameof(TokenBucketPolicyOptions.TokensPerPeriod)}' must be at least 1.");
        }

        if (policy.ReplenishmentPeriod <= TimeSpan.Zero)
        {
            failures.Add($"{prefix}:{nameof(TokenBucketPolicyOptions.ReplenishmentPeriod)}' must be greater than zero.");
        }
    }
}