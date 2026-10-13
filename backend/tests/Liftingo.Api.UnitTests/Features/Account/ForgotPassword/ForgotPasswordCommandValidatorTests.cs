using FluentValidation.Results;

using Liftingo.Application.Features.Account.ForgotPassword;

namespace Liftingo.Api.UnitTests.Features.Account.ForgotPassword;

public class ForgotPasswordCommandValidatorTests
{
    private static readonly ForgotPasswordCommandValidator s_validator = new();

    [Fact]
    public async Task Validate_ValidEmail_Passes()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            new ForgotPasswordCommand("anna@example.pl"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_EmailWithSurroundingWhitespace_IsTrimmedBeforeChecks()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            new ForgotPasswordCommand("  anna@example.pl \t"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Validation.EmailRequired")]
    [InlineData("   ", "Validation.EmailRequired")]
    [InlineData("not-an-email", "Validation.EmailInvalid")]
    public async Task Validate_BadEmail_FailsWithExpectedCode(string email, string code)
    {
        ValidationResult result = await s_validator.ValidateAsync(
            new ForgotPasswordCommand(email),
            TestContext.Current.CancellationToken);

        Assert.Equal(code, Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_EmailLongerThan256Chars_FailsWithEmailTooLong()
    {
        string email = new string('a', 257 - "@example.pl".Length) + "@example.pl";

        ValidationResult result = await s_validator.ValidateAsync(
            new ForgotPasswordCommand(email),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, e => e.ErrorCode == "Validation.EmailTooLong");
    }
}
