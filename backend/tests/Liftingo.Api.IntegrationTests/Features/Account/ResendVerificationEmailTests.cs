using System.Net;

using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Infrastructure.Email;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class ResendVerificationEmailTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Resend_UnverifiedAccount_Returns202AndSendsVerificationOnlyEmail()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, string firstToken) = await VerifyEmailTests.RegisterAsync(client, Factory);

        using HttpResponseMessage response = await ResendAsync(client, session.AccessToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        EmailMessage message = Assert.Single(Factory.Emails.Messages);
        Assert.Equal(AccountEmailRenderer.VerificationTemplate, message.TemplateName);
        Assert.Equal(session.Account.Id, message.UserId);
        Assert.Equal(session.Account.Email, message.To);
        Assert.DoesNotContain("Twoje konto jest gotowe", message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Twoje konto jest gotowe", message.HtmlBody, StringComparison.Ordinal);

        // The new token works, the one from registration was superseded.
        string newToken = AuthTestClient.ExtractToken(message);
        Assert.NotEqual(firstToken, newToken);
        using HttpResponseMessage old = await client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token = firstToken });
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        using HttpResponseMessage fresh = await client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token = newToken });
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    [Fact]
    public async Task Resend_ThirdTokenWithinTheHour_Returns202AndSendsNothing()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await VerifyEmailTests.RegisterAsync(client, Factory);

        // The registration token counts: registration + 2 resends = 3 tokens, so the next resend is over the limit.
        (await ResendAsync(client, session.AccessToken)).Dispose();
        (await ResendAsync(client, session.AccessToken)).Dispose();
        Assert.Equal(2, Factory.Emails.Messages.Count);

        using HttpResponseMessage third = await ResendAsync(client, session.AccessToken);

        Assert.Equal(HttpStatusCode.Accepted, third.StatusCode);
        Assert.Equal(2, Factory.Emails.Messages.Count);
    }

    [Fact]
    public async Task Resend_AfterTheHour_SendsAgain()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        using AuthTestClient client = factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await VerifyEmailTests.RegisterAsync(client, factory);
        (await ResendAsync(client, session.AccessToken)).Dispose();
        (await ResendAsync(client, session.AccessToken)).Dispose();
        (await ResendAsync(client, session.AccessToken)).Dispose();
        Assert.Equal(2, factory.Emails.Messages.Count);

        // The access token lives 15 minutes, so renew it after the hour has passed.
        factory.Time.Advance(TimeSpan.FromMinutes(61));
        using HttpResponseMessage refresh = await client.RefreshAsync();
        AuthSessionResponse renewed = await AuthTestClient.ReadSessionAsync(refresh);

        using HttpResponseMessage response = await ResendAsync(client, renewed.AccessToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(3, factory.Emails.Messages.Count);
    }

    [Fact]
    public async Task Resend_AlreadyVerifiedAccount_Returns202AndSendsNothing()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, string token) = await VerifyEmailTests.RegisterAsync(client, Factory);
        (await client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token })).Dispose();

        using HttpResponseMessage response = await ResendAsync(client, session.AccessToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(Factory.Emails.Messages);
    }

    [Fact]
    public async Task Resend_NoBearer_Returns401()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.PostJsonAsync(RouteConsts.Auth.ResendVerificationEmail, body: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(Factory.Emails.Messages);
    }

    private static Task<HttpResponseMessage> ResendAsync(AuthTestClient client, string accessToken) =>
        client.PostJsonAsync(RouteConsts.Auth.ResendVerificationEmail, body: null, accessToken);
}
