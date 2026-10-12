using System.Globalization;
using System.Net;
using System.Text;

using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog.Events;

namespace Liftingo.Api.IntegrationTests.Features.Account;

/// <summary>
/// FR-030: no e-mail, password, token or IP address in a log event. Each test builds its own host, because
/// Serilog's bootstrap logger is process-wide and a shared host's collector would not see these events.
/// </summary>
public class LogPrivacyTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ClientIp = "203.0.113.77";
    private const string Password = "Sztanga1!";
    private const string NewPassword = "Nowe1haslo!";
    private const string WrongPassword = "Zle-haslo1!";

    [Fact]
    public async Task Flows_RegisterLoginRefreshLogoutVerifyForgotAndReset_LogNoPersonalDataAtInformationAndAbove()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        string localPart = $"privacy{Guid.NewGuid():N}";
        string email = $"{localPart}@privacy-check.pl";
        using AuthTestClient client = factory.CreateAuthClient();
        var secrets = new List<string> { email, localPart, "@privacy-check.pl", Password, NewPassword, WrongPassword, ClientIp };

        (HttpResponseMessage registered, _) = await client.RegisterAsync(email, Password, Guid.NewGuid(), ClientIp);
        using HttpResponseMessage registeredOwned = registered;
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        AuthSessionResponse registeredSession = await AuthTestClient.ReadSessionAsync(registered);
        secrets.Add(registeredSession.AccessToken);
        secrets.Add(AuthTestClient.ReadRefreshCookie(registered)!.Value);
        string verificationToken = AuthTestClient.ExtractToken(Assert.Single(factory.Emails.Messages));
        secrets.Add(verificationToken);

        // Good, bad and unknown logins.
        using HttpResponseMessage good = await client.LoginAsync(email, Password, Guid.NewGuid(), ClientIp);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(good);
        secrets.Add(session.AccessToken);
        string rotatedAway = AuthTestClient.ReadRefreshCookie(good)!.Value;
        secrets.Add(rotatedAway);
        (await client.LoginAsync(email, WrongPassword, clientIp: ClientIp)).Dispose();
        (await client.LoginAsync($"unknown{Guid.NewGuid():N}@privacy-check.pl", WrongPassword, clientIp: ClientIp)).Dispose();

        // Refresh, reuse of the old cookie, logout.
        using HttpResponseMessage refreshed = await client.RefreshAsync(ClientIp);
        secrets.Add((await AuthTestClient.ReadSessionAsync(refreshed)).AccessToken);
        secrets.Add(AuthTestClient.ReadRefreshCookie(refreshed)!.Value);
        (await client.RefreshWithCookieAsync(rotatedAway, ClientIp)).Dispose();
        (await client.LogoutAsync(ClientIp)).Dispose();

        // Verify, forgot, reset.
        (await client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token = verificationToken }, clientIp: ClientIp)).Dispose();
        factory.Emails.Clear();
        (await client.PostJsonAsync(RouteConsts.Auth.ForgotPassword, new { email }, clientIp: ClientIp)).Dispose();
        string resetToken = AuthTestClient.ExtractToken(Assert.Single(factory.Emails.Messages));
        secrets.Add(resetToken);
        using HttpResponseMessage reset = await client.PostJsonAsync(
            RouteConsts.Auth.ResetPassword,
            new { token = resetToken, newPassword = NewPassword },
            clientIp: ClientIp);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        IReadOnlyList<LogEvent> events = [.. factory.Logs.Events.Where(e => e.Level >= LogEventLevel.Information)];

        // The sweep is only worth something if the flows really logged: EF and security events.
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.MessageTemplate.Text.StartsWith("Security event", StringComparison.Ordinal));
        Assert.Contains(events, e => e.MessageTemplate.Text.StartsWith("Executed DbCommand", StringComparison.Ordinal));

        foreach (LogEvent logEvent in events)
        {
            string rendered = Render(logEvent);

            foreach (string secret in secrets)
            {
                Assert.False(
                    rendered.Contains(secret, StringComparison.OrdinalIgnoreCase),
                    $"A log event leaked personal data. Template: {logEvent.MessageTemplate.Text}");
            }
        }
    }

    [Fact]
    public async Task Enricher_EmailLoggedOnPurpose_IsMaskedInTheCapturedEvent()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        const string email = "enricher-check@privacy-check.pl";
        using AuthTestClient client = factory.CreateAuthClient();

        // Starts the host (and with it the logging pipeline).
        (await client.LoginAsync("anyone@privacy-check.pl", WrongPassword)).Dispose();
        ILogger<LogPrivacyTests> logger = factory.Services.GetRequiredService<ILogger<LogPrivacyTests>>();

#pragma warning disable CA1848, CA1873 // The point is to log a raw e-mail on purpose.
        logger.LogInformation("Probe property {Email}", email);
        logger.LogInformation("Probe message: mail to {Recipient} failed", email);
#pragma warning restore CA1848, CA1873

        List<LogEvent> probes = [.. factory.Logs.Events.Where(e => e.MessageTemplate.Text.StartsWith("Probe", StringComparison.Ordinal))];
        Assert.Equal(2, probes.Count);
        Assert.All(probes, probe => Assert.DoesNotContain("enricher-check", Render(probe), StringComparison.OrdinalIgnoreCase));
        Assert.All(probes, probe => Assert.DoesNotContain("privacy-check.pl", Render(probe), StringComparison.OrdinalIgnoreCase));
    }

    private static string Render(LogEvent logEvent)
    {
        var text = new StringBuilder();
        text.Append(logEvent.RenderMessage(CultureInfo.InvariantCulture)).Append('\n');

        foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
        {
            text.Append(property.Key).Append('=').Append(property.Value.ToString()).Append('\n');
        }

        if (logEvent.Exception is not null)
        {
            text.Append(logEvent.Exception).Append('\n');
        }

        return text.ToString();
    }
}
