using FluentValidation.Results;

using Liftingo.Application.Features.Account;

namespace Liftingo.Api.UnitTests.Features.Account;

public class ConfirmProviderLinkValidatorTests
{
    private static readonly ConfirmProviderLinkValidator s_validator = new();

    [Fact]
    public async Task Validate_TokenOnly_Passes()
    {
        // The UI sends the token alone first and adds the consents when the API asks for them.
        ValidationResult result = await s_validator.ValidateAsync(
            new ConfirmProviderLinkRequest("a-token", null, null, null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_TokenWithDeclinedConsents_Passes()
    {
        // Whether the consents are needed depends on the account, so the handler judges them, not the validator.
        ValidationResult result = await s_validator.ValidateAsync(
            new ConfirmProviderLinkRequest("a-token", false, false, false),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Validate_MissingToken_FailsWithTokenRequired(string? token)
    {
        ValidationResult result = await s_validator.ValidateAsync(
            new ConfirmProviderLinkRequest(token!, true, true, true),
            TestContext.Current.CancellationToken);

        Assert.Equal("Validation.TokenRequired", Assert.Single(result.Errors).ErrorCode);
    }
}
