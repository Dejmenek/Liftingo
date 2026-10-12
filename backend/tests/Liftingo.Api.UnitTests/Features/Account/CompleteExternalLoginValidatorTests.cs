using FluentValidation.Results;

using Liftingo.Application.Features.Account;

namespace Liftingo.Api.UnitTests.Features.Account;

public class CompleteExternalLoginValidatorTests
{
    private static readonly CompleteExternalLoginValidator s_validator = new();

    [Fact]
    public async Task Validate_ConsentsAndNoEmail_Passes()
    {
        ValidationResult result = await s_validator.ValidateAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_TermsNotAccepted_FailsWithTermsRequired()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(acceptTermsAndAge: false),
            TestContext.Current.CancellationToken);

        Assert.Equal("Validation.TermsRequired", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_HealthDataNotAccepted_FailsWithHealthDataRequired()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(acceptHealthData: false),
            TestContext.Current.CancellationToken);

        Assert.Equal("Validation.HealthDataRequired", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_MarketingDeclined_Passes()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(acceptMarketing: false),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_AbsentEmail_Passes(string? email)
    {
        ValidationResult result = await s_validator.ValidateAsync(Request(email: email), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_EmailWithSurroundingSpaces_IsTrimmedBeforeChecking()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(email: "  anna@example.pl  "),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("anna@")]
    public async Task Validate_MalformedEmail_FailsWithEmailInvalid(string email)
    {
        ValidationResult result = await s_validator.ValidateAsync(Request(email: email), TestContext.Current.CancellationToken);

        Assert.Equal("Validation.EmailInvalid", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_EmailOf257Chars_FailsWithEmailTooLong()
    {
        string email = new string('a', 257 - "@example.pl".Length) + "@example.pl";

        ValidationResult result = await s_validator.ValidateAsync(Request(email: email), TestContext.Current.CancellationToken);

        Assert.Equal("Validation.EmailTooLong", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_EmailOf256Chars_Passes()
    {
        string email = new string('a', 256 - "@example.pl".Length) + "@example.pl";

        ValidationResult result = await s_validator.ValidateAsync(Request(email: email), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    private static CompleteExternalLoginRequest Request(
        bool acceptTermsAndAge = true,
        bool acceptHealthData = true,
        bool acceptMarketing = true,
        string? email = null) =>
        new(acceptTermsAndAge, acceptHealthData, acceptMarketing, email);
}
