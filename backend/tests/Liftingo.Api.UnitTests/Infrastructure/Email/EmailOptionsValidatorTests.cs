using Liftingo.Infrastructure.Email;

using Microsoft.Extensions.Options;

namespace Liftingo.Api.UnitTests.Infrastructure.Email;

public class EmailOptionsValidatorTests
{
    private readonly EmailOptionsValidator _validator = new();

    [Fact]
    public void Validate_DefaultZone_Succeeds()
    {
        ValidateOptionsResult result = _validator.Validate(null, new EmailOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_UnknownZone_FailsWithConfigPath()
    {
        ValidateOptionsResult result = _validator.Validate(null, new EmailOptions { DisplayTimeZone = "Mars/Olympus" });

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Email:DisplayTimeZone", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyZone_FailsWithConfigPath(string zone)
    {
        ValidateOptionsResult result = _validator.Validate(null, new EmailOptions { DisplayTimeZone = zone });

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Email:DisplayTimeZone", failure, StringComparison.Ordinal);
    }
}
