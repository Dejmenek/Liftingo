using System.Globalization;
using System.Net;

using Liftingo.Api.Endpoints;
using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Api.IntegrationTests.Infrastructure.Fakers;
using Liftingo.Application.Common.Security;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Email;

using Microsoft.Extensions.DependencyInjection;

using ApplicationDbContext = Liftingo.Infrastructure.Persistence.ApplicationDbContext;

namespace Liftingo.Api.IntegrationTests.Features.Account;

/// <summary>
/// The welcome and new-device e-mails. External-provider flows are tested manually: there is no fake provider
/// handler, so the provider sign-up cases (welcome-only e-mail, typed provider e-mail) and provider sign-in from a new
/// device are left out. The provider link confirmation is driven by staging its token directly.
/// </summary>
public class AccountEmailTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ChromeWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";

    private const string SafariIPhone =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    [Fact]
    public async Task Register_NewAccount_SendsExactlyOneVerificationAndWelcomeEmail()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        (HttpResponseMessage response, _) = await client.RegisterAsync(deviceId: Guid.NewGuid());
        response.Dispose();

        EmailMessage message = Assert.Single(Factory.Emails.Messages);
        Assert.Equal(AccountEmailRenderer.VerificationAndWelcomeTemplate, message.TemplateName);
    }

    [Fact]
    public async Task Login_NewDevice_SendsOneNewDeviceEmailWithTimeDeviceAndAdvice()
    {
        (_, string email, string password) = await RegisterAsync(Guid.NewGuid());
        using AuthTestClient clientB = Factory.CreateAuthClient();

        using HttpResponseMessage login = await clientB.LoginAsync(email, password, Guid.NewGuid(), userAgent: ChromeWindows);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        EmailMessage message = Assert.Single(Factory.Emails.Messages);
        Assert.Equal(AccountEmailRenderer.NewDeviceTemplate, message.TemplateName);
        Assert.Equal(email, message.To);

        string expectedTime = TimeZoneInfo
            .ConvertTime(Factory.Time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw"))
            .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        Assert.Contains(expectedTime, message.TextBody, StringComparison.Ordinal);
        Assert.Contains("Chrome, Windows, komputer", message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"{ApiFactory.FrontendBaseUrl}/account/sessions", message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"{ApiFactory.FrontendBaseUrl}/auth/forgot-password", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("Chrome, Windows, komputer", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_SameNewDeviceAgain_SendsNoSecondEmail()
    {
        (_, string email, string password) = await RegisterAsync(Guid.NewGuid());
        Guid deviceB = Guid.NewGuid();
        using AuthTestClient clientB = Factory.CreateAuthClient();
        (await clientB.LoginAsync(email, password, deviceB, userAgent: SafariIPhone)).Dispose();
        Factory.Emails.Clear();

        using HttpResponseMessage again = await clientB.LoginAsync(email, password, deviceB, userAgent: SafariIPhone);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Empty(Factory.Emails.Messages);
    }

    [Fact]
    public async Task Login_FailedFromNewDevice_SendsNoEmail()
    {
        (_, string email, _) = await RegisterAsync(Guid.NewGuid());
        using AuthTestClient clientB = Factory.CreateAuthClient();

        using HttpResponseMessage login = await clientB.LoginAsync(email, "Zle1haslo!", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Empty(Factory.Emails.Messages);
    }

    [Fact]
    public async Task Login_RightAfterRegistrationOnTheSameDevice_SendsNoNewDeviceEmail()
    {
        Guid device = Guid.NewGuid();
        (_, string email, string password) = await RegisterAsync(device);
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage login = await client.LoginAsync(email, password, device);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Empty(Factory.Emails.Messages);
    }

    [Fact]
    public async Task Refresh_AfterANewDeviceLogin_SendsNoEmail()
    {
        (_, string email, string password) = await RegisterAsync(Guid.NewGuid());
        using AuthTestClient clientB = Factory.CreateAuthClient();
        (await clientB.LoginAsync(email, password, Guid.NewGuid())).Dispose();
        Factory.Emails.Clear();

        using HttpResponseMessage refresh = await clientB.RefreshAsync();

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Empty(Factory.Emails.Messages);
    }

    [Fact]
    public async Task Login_FormerlyKnownDeviceAfterLogoutAll_SendsNewDeviceEmail()
    {
        Guid device = Guid.NewGuid();
        (AuthSessionResponse session, string email, string password) = await RegisterAsync(device);
        using AuthTestClient client = Factory.CreateAuthClient();
        (await client.LogoutAllAsync(session.AccessToken)).Dispose();
        Factory.Emails.Clear();

        using AuthTestClient again = Factory.CreateAuthClient();
        using HttpResponseMessage login = await again.LoginAsync(email, password, device);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(AccountEmailRenderer.NewDeviceTemplate, Assert.Single(Factory.Emails.Messages).TemplateName);
    }

    [Fact]
    public async Task Login_FormerlyKnownDeviceAfterPasswordReset_SendsNewDeviceEmail()
    {
        Guid device = Guid.NewGuid();
        using AuthTestClient client = Factory.CreateAuthClient();
        (HttpResponseMessage registered, RegisterRequestBody request) = await client.RegisterAsync(deviceId: device);
        registered.Dispose();
        string verifyToken = AuthTestClient.ExtractToken(Assert.Single(Factory.Emails.Messages));
        (await client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token = verifyToken })).Dispose();
        Factory.Emails.Clear();

        (await client.PostJsonAsync(RouteConsts.Auth.ForgotPassword, new { email = request.Email })).Dispose();
        string resetToken = AuthTestClient.ExtractToken(Assert.Single(Factory.Emails.Messages));
        const string newPassword = "Nowe1haslo!";
        (await client.PostJsonAsync(RouteConsts.Auth.ResetPassword, new { token = resetToken, newPassword })).Dispose();
        Factory.Emails.Clear();

        using HttpResponseMessage login = await client.LoginAsync(request.Email, newPassword, device);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(AccountEmailRenderer.NewDeviceTemplate, Assert.Single(Factory.Emails.Messages).TemplateName);
    }

    [Fact]
    public async Task ConfirmProviderLink_FromANewDevice_SendsNewDeviceEmail()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (HttpResponseMessage registered, RegisterRequestBody request) = await client.RegisterAsync(deviceId: Guid.NewGuid());
        using HttpResponseMessage owned = registered;
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(registered);
        string verifyToken = AuthTestClient.ExtractToken(Assert.Single(Factory.Emails.Messages));
        (await client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token = verifyToken })).Dispose();
        Factory.Emails.Clear();

        string linkToken = await StageProviderLinkTokenAsync(session.Account.Id);

        using AuthTestClient other = Factory.CreateAuthClient();
        using HttpResponseMessage confirm = await other.PostJsonAsync(RouteConsts.Auth.ConfirmProviderLink, new { token = linkToken });

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        EmailMessage message = Assert.Single(Factory.Emails.Messages);
        Assert.Equal(AccountEmailRenderer.NewDeviceTemplate, message.TemplateName);
        Assert.Equal(request.Email, message.To);
    }

    private async Task<(AuthSessionResponse Session, string Email, string Password)> RegisterAsync(Guid deviceId)
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (HttpResponseMessage response, RegisterRequestBody request) = await client.RegisterAsync(deviceId: deviceId);
        using HttpResponseMessage owned = response;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);
        Factory.Emails.Clear();

        return (session, request.Email, request.Password);
    }

    private async Task<string> StageProviderLinkTokenAsync(Guid userId)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (string token, byte[] hash) = OpaqueToken.Generate();

        db.EmailTokens.Add(EmailToken.IssueProviderLink(
            userId,
            hash,
            "Google",
            "google-key-" + Guid.NewGuid().ToString("N"),
            Factory.Time.GetUtcNow(),
            TimeSpan.FromHours(24)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return token;
    }
}
