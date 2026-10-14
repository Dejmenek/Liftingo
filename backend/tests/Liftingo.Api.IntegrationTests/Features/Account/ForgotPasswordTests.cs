using Liftingo.Api.Endpoints;
using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using System.Net;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class ForgotPasswordTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Forgot_RegisteredAndUnregisteredEmail_GiveTheSameStatusAndBody()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await VerifyEmailTests.RegisterAsync(client, Factory);

        using HttpResponseMessage registered = await ForgotAsync(client, session.Account.Email);
        using HttpResponseMessage unregistered = await ForgotAsync(client, "nikt@example.pl");

        Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unregistered.StatusCode);
        string registeredBody = await registered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string unregisteredBody = await unregistered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(registeredBody, unregisteredBody);
        Assert.Empty(registeredBody);
    }

    [Fact]
    public async Task Forgot_RegisteredEmail_SendsOneEmailWithAOneHourToken()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await VerifyEmailTests.RegisterAsync(client, Factory);

        // Trimmed and case-insensitive, like login.
        (await ForgotAsync(client, $"  {session.Account.Email.ToUpperInvariant()} ")).Dispose();
        (await ForgotAsync(client, "nikt@example.pl")).Dispose();

        EmailMessage message = Assert.Single(Factory.Emails.Messages);
        Assert.Equal(AccountEmailRenderer.PasswordResetTemplate, message.TemplateName);
        Assert.Equal(session.Account.Id, message.UserId);
        Assert.Equal(session.Account.Email, message.To);
        Assert.Contains($"{ApiFactory.FrontendBaseUrl}/auth/reset-password?token=", message.TextBody, StringComparison.Ordinal);

        EmailToken stored = await WithDbAsync(db => db.EmailTokens
            .SingleAsync(t => t.UserId == session.Account.Id && t.Purpose == EmailTokenPurpose.PasswordReset, TestContext.Current.CancellationToken));
        Assert.Equal(TimeSpan.FromHours(1), stored.ExpiresAt - stored.CreatedAt);
    }

    [Fact]
    public async Task Forgot_FourthRequestWithinTheHour_Returns202AndSendsNothing()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await VerifyEmailTests.RegisterAsync(client, Factory);

        for (int request = 1; request <= 3; request++)
        {
            (await ForgotAsync(client, session.Account.Email)).Dispose();
        }

        Assert.Equal(3, Factory.Emails.Messages.Count);

        using HttpResponseMessage fourth = await ForgotAsync(client, session.Account.Email);

        Assert.Equal(HttpStatusCode.Accepted, fourth.StatusCode);
        Assert.Equal(3, Factory.Emails.Messages.Count);
    }

    [Fact]
    public async Task Forgot_NewRequest_SupersedesTheEarlierToken()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, string verificationToken) = await VerifyEmailTests.RegisterAsync(client, Factory);

        // Verified, so the reset below needs no consents.
        (await client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token = verificationToken })).Dispose();
        (await ForgotAsync(client, session.Account.Email)).Dispose();
        (await ForgotAsync(client, session.Account.Email)).Dispose();
        string first = AuthTestClient.ExtractToken(Factory.Emails.Messages[0]);
        string second = AuthTestClient.ExtractToken(Factory.Emails.Messages[1]);

        using HttpResponseMessage withFirst = await client.PostJsonAsync(
            RouteConsts.Auth.ResetPassword,
            new { token = first, newPassword = "Nowe1haslo!" });
        using HttpResponseMessage withSecond = await client.PostJsonAsync(
            RouteConsts.Auth.ResetPassword,
            new { token = second, newPassword = "Nowe1haslo!" });

        Assert.Equal(HttpStatusCode.BadRequest, withFirst.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withSecond.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Forgot_MalformedEmail_Returns400(string email)
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await ForgotAsync(client, email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Factory.Emails.Messages);
    }

    [Fact]
    public async Task Forgot_EmailSenderThrows_StillReturns202AndLogsTemplateAndUserIdOnly()
    {
        // A host of its own: Serilog's bootstrap logger is process-wide and follows the newest host.
        using ApiFactory factory = new(Fixture.ConnectionString, Fixture.RedisConnectionString);
        using AuthTestClient client = factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await VerifyEmailTests.RegisterAsync(client, factory);
        factory.Emails.ThrowOnSend = true;

        using HttpResponseMessage response = await ForgotAsync(client, session.Account.Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        LogEvent failure = Assert.Single(
            factory.Logs.Events,
            e => PropertyValue(e, "TemplateName") is string name && name == AccountEmailRenderer.PasswordResetTemplate);
        Assert.Equal(session.Account.Id, PropertyValue(failure, "UserId"));
        Assert.DoesNotContain(
            factory.Logs.Events,
            e => e.RenderMessage(null).Contains(session.Account.Email, StringComparison.OrdinalIgnoreCase));
    }

    private static Task<HttpResponseMessage> ForgotAsync(AuthTestClient client, string email) =>
        client.PostJsonAsync(RouteConsts.Auth.ForgotPassword, new { email });

    private static object? PropertyValue(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out LogEventPropertyValue? value) && value is ScalarValue scalar
            ? scalar.Value
            : null;
}