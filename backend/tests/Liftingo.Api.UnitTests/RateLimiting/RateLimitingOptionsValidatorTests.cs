using Liftingo.Api.RateLimiting;

using Microsoft.Extensions.Options;

namespace Liftingo.Api.UnitTests.RateLimiting;

public class RateLimitingOptionsValidatorTests
{
    private readonly RateLimitingOptionsValidator _sut = new();

    [Fact]
    public void Validate_ValidOptions_Succeeds()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_DefaultOptions_FailsForEveryValueOfBothPolicies()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, new RateLimitingOptions());

        Assert.True(result.Failed);
        Assert.Equal(6, result.Failures.Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_AnonymousTokenLimitBelowOne_FailsWithConfigPath(int tokenLimit)
    {
        RateLimitingOptions options = CreateOptions(anonymous: CreatePolicy(tokenLimit: tokenLimit));

        ValidateOptionsResult result = _sut.Validate(name: null, options);

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("RateLimiting:Anonymous:TokenLimit", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AuthenticatedTokensPerPeriodBelowOne_FailsWithConfigPath()
    {
        RateLimitingOptions options = CreateOptions(authenticated: CreatePolicy(tokensPerPeriod: 0));

        ValidateOptionsResult result = _sut.Validate(name: null, options);

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("RateLimiting:Authenticated:TokensPerPeriod", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReplenishmentPeriodNotPositive_FailsWithConfigPath()
    {
        RateLimitingOptions options = CreateOptions(anonymous: CreatePolicy(replenishmentPeriod: TimeSpan.Zero));

        ValidateOptionsResult result = _sut.Validate(name: null, options);

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("RateLimiting:Anonymous:ReplenishmentPeriod", failure, StringComparison.Ordinal);
    }

    private static RateLimitingOptions CreateOptions(
        TokenBucketPolicyOptions? authenticated = null,
        TokenBucketPolicyOptions? anonymous = null) =>
        new()
        {
            Authenticated = authenticated ?? CreatePolicy(),
            Anonymous = anonymous ?? CreatePolicy(),
        };

    private static TokenBucketPolicyOptions CreatePolicy(
        int tokenLimit = 20,
        int tokensPerPeriod = 5,
        TimeSpan? replenishmentPeriod = null) =>
        new()
        {
            TokenLimit = tokenLimit,
            TokensPerPeriod = tokensPerPeriod,
            ReplenishmentPeriod = replenishmentPeriod ?? TimeSpan.FromSeconds(10),
        };
}