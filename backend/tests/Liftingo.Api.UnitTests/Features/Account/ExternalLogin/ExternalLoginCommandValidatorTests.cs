using FluentValidation.Results;

using Liftingo.Application.Features.Account.ExternalLogin;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Api.UnitTests.Features.Account.ExternalLogin;

public class ExternalLoginCommandValidatorTests
{
    private static readonly ExternalLoginCommandValidator s_validator = new();

    [Fact]
    public async Task Validate_TokenAndNothingElse_Passes()
    {
        ValidationResult result = await s_validator.ValidateAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyToken_FailsWithTokenRequired(string token)
    {
        ValidationResult result = await s_validator.ValidateAsync(Request(token: token), TestContext.Current.CancellationToken);

        Assert.Equal("Validation.TokenRequired", Assert.Single(result.Errors).ErrorCode);
    }

    [Fact]
    public async Task Validate_ConsentsDeclined_Passes()
    {
        // Whether consents are needed depends on the account, so the handler answers that, not the validator.
        ValidationResult result = await s_validator.ValidateAsync(
            Request(acceptTermsAndAge: false, acceptHealthData: false),
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

    private static ExternalLoginCommand Request(
        string token = "provider-token",
        bool? acceptTermsAndAge = null,
        bool? acceptHealthData = null,
        string? email = null) =>
        new(
            "google",
            token,
            acceptTermsAndAge,
            acceptHealthData,
            AcceptMarketing: null,
            email,
            new ClientInfo("203.0.113.5", "Chrome, Windows, komputer", Guid.NewGuid()));
}