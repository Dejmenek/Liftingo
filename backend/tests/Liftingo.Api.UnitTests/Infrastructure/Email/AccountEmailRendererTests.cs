using Liftingo.Application.Common.Configuration;
using Liftingo.Application.Common.Email;
using Liftingo.Application.Features.Account.Shared.Emails;
using Liftingo.Infrastructure.Email;

using Microsoft.Extensions.Options;

namespace Liftingo.Api.UnitTests.Infrastructure.Email;

public class AccountEmailRendererTests
{
    private const string FrontendBaseUrl = "https://app.liftingo.test";
    private const string Recipient = "anna@example.pl";

    private static readonly Guid UserId = Guid.NewGuid();

    private readonly AccountEmailRenderer _renderer = new(
        Options.Create(new AppOptions { FrontendBaseUrl = new Uri(FrontendBaseUrl + "/") }),
        Options.Create(new EmailOptions()));

    [Fact]
    public void Render_NewDeviceInJuly_ShowsWarsawSummerTime()
    {
        var signedInAt = new DateTimeOffset(2026, 7, 15, 10, 5, 0, TimeSpan.Zero);

        EmailMessage message = _renderer.Render(new NewDeviceEmail(UserId, Recipient, signedInAt, "Chrome, Windows, komputer"));

        // CEST is UTC+2.
        Assert.Contains("15.07.2026 12:05", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("15.07.2026 12:05", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_NewDeviceInJanuary_ShowsWarsawWinterTime()
    {
        var signedInAt = new DateTimeOffset(2026, 1, 15, 10, 5, 0, TimeSpan.Zero);

        EmailMessage message = _renderer.Render(new NewDeviceEmail(UserId, Recipient, signedInAt, "Chrome, Windows, komputer"));

        // CET is UTC+1.
        Assert.Contains("15.01.2026 11:05", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_NewDeviceWithMarkupInDeviceType_EncodesItInHtmlOnly()
    {
        EmailMessage message = _renderer.Render(new NewDeviceEmail(UserId, Recipient, DateTimeOffset.UtcNow, "<b>Edge</b> & co"));

        Assert.Contains("&lt;b&gt;Edge&lt;/b&gt; &amp; co", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Edge</b>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("<b>Edge</b> & co", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_NewDevice_LinksToSignOutOfAllDevicesAndPasswordReset()
    {
        EmailMessage message = _renderer.Render(new NewDeviceEmail(UserId, Recipient, DateTimeOffset.UtcNow, "Firefox, Linux, komputer"));

        Assert.Contains($"{FrontendBaseUrl}/account/sessions", message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"{FrontendBaseUrl}/auth/forgot-password", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ProviderLinkConfirmationWithMarkupInProvider_EncodesItInHtml()
    {
        EmailMessage message = _renderer.Render(new ProviderLinkConfirmationEmail(UserId, Recipient, "<X&Y>", "token"));

        Assert.Contains("&lt;X&amp;Y&gt;", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<X&Y>", message.HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AccountEmailRenderer.VerificationAndWelcomeTemplate, "auth/verify-email")]
    [InlineData(AccountEmailRenderer.VerificationTemplate, "auth/verify-email")]
    [InlineData(AccountEmailRenderer.PasswordResetTemplate, "auth/reset-password")]
    [InlineData(AccountEmailRenderer.ProviderLinkConfirmationTemplate, "auth/confirm-link")]
    public void Render_TokenEmail_BuildsFrontendLinkWithEscapedToken(string templateName, string path)
    {
        const string token = "a+b/c=";
        OutgoingEmail email = templateName switch
        {
            AccountEmailRenderer.VerificationAndWelcomeTemplate => new VerificationAndWelcomeEmail(UserId, Recipient, token),
            AccountEmailRenderer.VerificationTemplate => new VerificationEmail(UserId, Recipient, token),
            AccountEmailRenderer.PasswordResetTemplate => new PasswordResetEmail(UserId, Recipient, token),
            _ => new ProviderLinkConfirmationEmail(UserId, Recipient, "Google", token),
        };

        EmailMessage message = _renderer.Render(email);

        string expected = $"{FrontendBaseUrl}/{path}?token=a%2Bb%2Fc%3D";
        Assert.Contains(expected, message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"href=\"{expected}\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.Equal(templateName, message.TemplateName);
        Assert.Equal(Recipient, message.To);
        Assert.Equal(UserId, message.UserId);
    }

    [Fact]
    public void Render_Welcome_HasNoLink()
    {
        EmailMessage message = _renderer.Render(new WelcomeEmail(UserId, Recipient));

        Assert.Equal(AccountEmailRenderer.WelcomeTemplate, message.TemplateName);
        Assert.DoesNotContain("http", message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("href", message.HtmlBody, StringComparison.Ordinal);
    }
}
