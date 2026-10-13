using Liftingo.Application.Common.Configuration;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.Extensions.Options;

namespace Liftingo.Api.UnitTests.Common;

public class AccountOptionsValidatorTests
{
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    // Sessions

    [Fact]
    public void Validate_ValidSessions_Succeeds()
    {
        ValidateOptionsResult result = new SessionsOptionsValidator().Validate(null, ValidSessions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_DefaultSessions_FailsForBothTimeouts()
    {
        ValidateOptionsResult result = new SessionsOptionsValidator().Validate(null, new SessionsOptions());

        Assert.Contains(result.Failures!, f => f.Contains("Sessions:InactivityTimeout", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, f => f.Contains("Sessions:AbsoluteLifetime", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AbsoluteLifetimeShorterThanInactivityTimeout_Fails()
    {
        var options = new SessionsOptions { InactivityTimeout = 30 * Day, AbsoluteLifetime = 29 * Day };

        ValidateOptionsResult result = new SessionsOptionsValidator().Validate(null, options);

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Sessions:AbsoluteLifetime", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AbsoluteLifetimeEqualToInactivityTimeout_Succeeds()
    {
        var options = new SessionsOptions { InactivityTimeout = 30 * Day, AbsoluteLifetime = 30 * Day };

        ValidateOptionsResult result = new SessionsOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    // KnownDevices

    [Fact]
    public void Validate_KnownDevicesRetentionPositive_Succeeds()
    {
        ValidateOptionsResult result = new KnownDevicesOptionsValidator().Validate(null, new KnownDevicesOptions { Retention = 365 * Day });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_KnownDevicesRetentionZero_FailsWithConfigPath()
    {
        ValidateOptionsResult result = new KnownDevicesOptionsValidator().Validate(null, new KnownDevicesOptions());

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("KnownDevices:Retention", failure, StringComparison.Ordinal);
    }

    // EmailTokens

    [Fact]
    public void Validate_ValidEmailTokens_Succeeds()
    {
        ValidateOptionsResult result = new EmailTokensOptionsValidator().Validate(null, ValidEmailTokens());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_DefaultEmailTokens_FailsForEveryValue()
    {
        ValidateOptionsResult result = new EmailTokensOptionsValidator().Validate(null, new EmailTokensOptions());

        Assert.Equal(5, result.Failures!.Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxPerAddressPerHourBelowOne_FailsWithConfigPath(int value)
    {
        EmailTokensOptions options = ValidEmailTokens(maxPerAddressPerHour: value);

        ValidateOptionsResult result = new EmailTokensOptionsValidator().Validate(null, options);

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("EmailTokens:MaxPerAddressPerHour", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PasswordResetLifetimeZero_FailsWithConfigPath()
    {
        EmailTokensOptions options = ValidEmailTokens(passwordResetLifetime: TimeSpan.Zero);

        ValidateOptionsResult result = new EmailTokensOptionsValidator().Validate(null, options);

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("EmailTokens:PasswordResetLifetime", failure, StringComparison.Ordinal);
    }

    // ExternalLogin

    [Fact]
    public void Validate_ExternalLoginPendingTimeoutPositive_Succeeds()
    {
        ValidateOptionsResult result = new ExternalLoginOptionsValidator()
            .Validate(null, new ExternalLoginOptions { PendingTimeout = TimeSpan.FromMinutes(10) });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ExternalLoginPendingTimeoutZero_FailsWithConfigPath()
    {
        ValidateOptionsResult result = new ExternalLoginOptionsValidator().Validate(null, new ExternalLoginOptions());

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("ExternalLogin:PendingTimeout", failure, StringComparison.Ordinal);
    }

    // Audit

    [Fact]
    public void Validate_AuditRetentionPositive_Succeeds()
    {
        ValidateOptionsResult result = new AuditOptionsValidator().Validate(null, new AuditOptions { Retention = 90 * Day });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_AuditRetentionZero_FailsWithConfigPath()
    {
        ValidateOptionsResult result = new AuditOptionsValidator().Validate(null, new AuditOptions());

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Audit:Retention", failure, StringComparison.Ordinal);
    }

    // Legal

    [Fact]
    public void Validate_LegalVersionPresent_Succeeds()
    {
        ValidateOptionsResult result = new LegalOptionsValidator().Validate(null, new LegalOptions { DocumentsVersion = "2026-10-01" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_LegalVersionOf32Chars_Succeeds()
    {
        ValidateOptionsResult result = new LegalOptionsValidator()
            .Validate(null, new LegalOptions { DocumentsVersion = new string('v', 32) });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_LegalVersionEmpty_FailsWithConfigPath(string version)
    {
        ValidateOptionsResult result = new LegalOptionsValidator().Validate(null, new LegalOptions { DocumentsVersion = version });

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Legal:DocumentsVersion", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_LegalVersionLongerThan32Chars_FailsWithConfigPath()
    {
        ValidateOptionsResult result = new LegalOptionsValidator()
            .Validate(null, new LegalOptions { DocumentsVersion = new string('v', 33) });

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("Legal:DocumentsVersion", failure, StringComparison.Ordinal);
    }

    // App

    public static TheoryData<Uri> ValidFrontendUrls =>
        [new Uri("https://localhost:4200"), new Uri("https://app.liftingo.example/")];

    public static TheoryData<Uri> InvalidFrontendUrls =>
        [new Uri("/relative/path", UriKind.Relative), new Uri("http://localhost:4200"), new Uri("ftp://localhost:4200")];

    [Theory]
    [MemberData(nameof(ValidFrontendUrls))]
    public void Validate_AbsoluteHttpsFrontendUrl_Succeeds(Uri url)
    {
        ValidateOptionsResult result = new AppOptionsValidator().Validate(null, new AppOptions { FrontendBaseUrl = url });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_FrontendUrlMissing_FailsWithConfigPath()
    {
        ValidateOptionsResult result = new AppOptionsValidator().Validate(null, new AppOptions());

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("App:FrontendBaseUrl", failure, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidFrontendUrls))]
    public void Validate_FrontendUrlNotAbsoluteHttps_FailsWithConfigPath(Uri url)
    {
        ValidateOptionsResult result = new AppOptionsValidator().Validate(null, new AppOptions { FrontendBaseUrl = url });

        string failure = Assert.Single(result.Failures!);
        Assert.Contains("App:FrontendBaseUrl", failure, StringComparison.Ordinal);
    }

    private static SessionsOptions ValidSessions() =>
        new() { InactivityTimeout = 30 * Day, AbsoluteLifetime = 90 * Day };

    private static EmailTokensOptions ValidEmailTokens(
        TimeSpan? passwordResetLifetime = null,
        int maxPerAddressPerHour = 3) =>
        new()
        {
            VerificationLifetime = Day,
            PasswordResetLifetime = passwordResetLifetime ?? TimeSpan.FromHours(1),
            ProviderLinkLifetime = Day,
            MaxPerAddressPerHour = maxPerAddressPerHour,
            RetentionAfterUnusable = Day,
        };
}
