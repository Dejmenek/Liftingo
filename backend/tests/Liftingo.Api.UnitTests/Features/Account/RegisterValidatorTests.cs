using FluentValidation.Results;

using Liftingo.Application.Features.Account;

namespace Liftingo.Api.UnitTests.Features.Account;

public class RegisterValidatorTests
{
    private const string ValidPassword = "Sztanga1!";

    private static readonly RegisterValidator s_validator = new();

    [Fact]
    public async Task Validate_ValidRequest_Passes()
    {
        ValidationResult result = await s_validator.ValidateAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_EmailWithSurroundingWhitespace_IsTrimmedBeforeFormatCheck()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(email: "  anna@example.pl \t"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_EmailOf256CharsPlusPadding_PassesLengthCheckAfterTrim()
    {
        string email = new string('a', 256 - "@example.pl".Length) + "@example.pl";

        ValidationResult result = await s_validator.ValidateAsync(
            Request(email: $"  {email}  "),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_EmailLongerThan256Chars_FailsWithEmailTooLong()
    {
        string email = new string('a', 257 - "@example.pl".Length) + "@example.pl";

        ValidationResult result = await s_validator.ValidateAsync(
            Request(email: email),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, e => e.ErrorCode == "Validation.EmailTooLong");
    }

    [Theory]
    [InlineData("", "Validation.EmailRequired")]
    [InlineData("   ", "Validation.EmailRequired")]
    [InlineData("not-an-email", "Validation.EmailInvalid")]
    public async Task Validate_BadEmail_FailsWithExpectedCode(string email, string code)
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(email: email),
            TestContext.Current.CancellationToken);

        Assert.Equal(code, Assert.Single(result.Errors).ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_MissingPassword_FailsWithPasswordRequired(string? password)
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(password: password!),
            TestContext.Current.CancellationToken);

        Assert.Equal("Validation.PasswordRequired", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_PasswordOf128Chars_Passes()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(password: new string('a', 126) + "1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_PasswordLongerThan128Chars_FailsWithPasswordTooLong()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(password: new string('a', 127) + "1!"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Validation.PasswordTooLong", Assert.Single(result.Errors).ErrorCode);
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
    public async Task Validate_HealthDataNotAccepted_FailsWithHealthDataRequiredExplainingBodyWeight()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(acceptHealthData: false),
            TestContext.Current.CancellationToken);

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal("Validation.HealthDataRequired", failure.ErrorCode);
        Assert.Contains("wagi ciała", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_MarketingNotAccepted_Passes()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(acceptMarketing: false),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    private static RegisterRequest Request(
        string email = "anna@example.pl",
        string password = ValidPassword,
        bool acceptTermsAndAge = true,
        bool acceptHealthData = true,
        bool acceptMarketing = false) =>
        new(email, password, acceptTermsAndAge, acceptHealthData, acceptMarketing);
}
