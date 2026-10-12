using FluentValidation.Results;

using Liftingo.Application.Features.Account;

namespace Liftingo.Api.UnitTests.Features.Account;

public class ResetPasswordValidatorTests
{
    private static readonly ResetPasswordValidator s_validator = new();

    [Fact]
    public async Task Validate_ValidRequest_Passes()
    {
        ValidationResult result = await s_validator.ValidateAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Validate_MissingToken_FailsWithTokenRequired(string? token)
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(token: token!),
            TestContext.Current.CancellationToken);

        Assert.Equal("Validation.TokenRequired", Assert.Single(result.Errors).ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_MissingNewPassword_FailsWithPasswordRequired(string? password)
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(newPassword: password!),
            TestContext.Current.CancellationToken);

        Assert.Equal("Validation.PasswordRequired", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_NewPasswordOf128Chars_Passes()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(newPassword: new string('a', 126) + "1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_NewPasswordLongerThan128Chars_FailsWithPasswordTooLong()
    {
        ValidationResult result = await s_validator.ValidateAsync(
            Request(newPassword: new string('a', 127) + "1!"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Validation.PasswordTooLong", Assert.Single(result.Errors).ErrorCode);
    }

    private static ResetPasswordRequest Request(string token = "abc", string newPassword = "Nowe1haslo!") =>
        new(token, newPassword, AcceptTermsAndAge: null, AcceptHealthData: null, AcceptMarketing: null);
}
