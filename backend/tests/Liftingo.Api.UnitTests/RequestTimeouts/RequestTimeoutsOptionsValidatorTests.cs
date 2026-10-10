using Liftingo.Api.RequestTimeouts;

using Microsoft.Extensions.Options;

namespace Liftingo.Api.UnitTests.RequestTimeouts;

public class RequestTimeoutsOptionsValidatorTests
{
    private readonly RequestTimeoutsOptionsValidator _sut = new();

    [Fact]
    public void Validate_ValidOptions_Succeeds()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NoNamedPolicies_Succeeds()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, CreateOptions(policies: []));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_DefaultOptions_FailsForDefaultTimeout()
    {
        ValidateOptionsResult result = _sut.Validate(name: null, new RequestTimeoutsOptions());

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("RequestTimeouts:DefaultTimeout", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_DefaultTimeoutNotPositive_FailsWithConfigPath(int seconds)
    {
        RequestTimeoutsOptions options = CreateOptions(defaultTimeout: TimeSpan.FromSeconds(seconds));

        ValidateOptionsResult result = _sut.Validate(name: null, options);

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("RequestTimeouts:DefaultTimeout", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PolicyTimeoutNotPositive_FailsWithPolicyName()
    {
        RequestTimeoutsOptions options = CreateOptions(policies: new() { ["AiGeneration"] = TimeSpan.Zero });

        ValidateOptionsResult result = _sut.Validate(name: null, options);

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("RequestTimeouts:Policies:AiGeneration", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_BlankPolicyName_Fails(string policyName)
    {
        RequestTimeoutsOptions options = CreateOptions(policies: new() { [policyName] = TimeSpan.FromMinutes(1) });

        ValidateOptionsResult result = _sut.Validate(name: null, options);

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("blank policy name", failure, StringComparison.Ordinal);
    }

    private static RequestTimeoutsOptions CreateOptions(
        TimeSpan? defaultTimeout = null,
        Dictionary<string, TimeSpan>? policies = null) =>
        new()
        {
            DefaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(30),
            Policies = policies ?? new() { ["AiGeneration"] = TimeSpan.FromMinutes(1) },
        };
}