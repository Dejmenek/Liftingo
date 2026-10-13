using System.Net;
using System.Text.Json;

using Liftingo.Api.Endpoints;
using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Audit;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class SecurityAuditTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ClientIp = "203.0.113.10";
    private const string Chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private const string Password = "Sztanga1!";
    private const string NewPassword = "Nowe1haslo!";
    private const string WrongPassword = "Zle-haslo1!";

    [Fact]
    public async Task Flows_EveryLoginAttemptAndListedEvent_WriteExactlyOneRecordEachInOrder()
    {
        string email = $"audit-{Guid.NewGuid():N}@example.pl";
        Guid deviceId = Guid.NewGuid();
        using AuthTestClient client = Factory.CreateAuthClient();

        (HttpResponseMessage registered, _) = await client.RegisterAsync(email, Password, deviceId, ClientIp);
        using HttpResponseMessage registeredOwned = registered;
        AuthSessionResponse registeredSession = await AuthTestClient.ReadSessionAsync(registered);
        RefreshCookie firstCookie = AuthTestClient.ReadRefreshCookie(registered)!;
        string verificationToken = AuthTestClient.ExtractToken(Assert.Single(Factory.Emails.Messages));
        Assert.Empty(Factory.Audit.Records);

        // Login: wrong password, unknown e-mail, right password.
        (await client.LoginAsync(email, WrongPassword, deviceId, ClientIp, Chrome)).Dispose();
        (await client.LoginAsync($"nikt-{Guid.NewGuid():N}@example.pl", WrongPassword, deviceId, ClientIp, Chrome)).Dispose();
        using HttpResponseMessage loggedIn = await client.LoginAsync(email, Password, deviceId, ClientIp, Chrome);
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(loggedIn);
        string rotatedAway = AuthTestClient.ReadRefreshCookie(loggedIn)!.Value;

        // Routine refreshes write nothing.
        int before = Factory.Audit.Records.Count;
        (await client.RefreshAsync(ClientIp)).Dispose();
        (await client.RefreshAsync(ClientIp)).Dispose();
        Assert.Equal(before, Factory.Audit.Records.Count);

        // Replaying a rotated cookie is reuse; then sign in again and out again.
        (await client.RefreshWithCookieAsync(rotatedAway, ClientIp)).Dispose();
        (await client.LoginAsync(email, Password, deviceId, ClientIp, Chrome)).Dispose();
        (await client.LogoutAsync(ClientIp)).Dispose();

        using HttpResponseMessage secondLogin = await client.LoginAsync(email, Password, deviceId, ClientIp, Chrome);
        AuthSessionResponse secondSession = await AuthTestClient.ReadSessionAsync(secondLogin);
        (await client.LogoutAllAsync(secondSession.AccessToken, ClientIp)).Dispose();

        // Verify, forgot and reset: only the reset is a security event.
        using HttpResponseMessage verified = await client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token = verificationToken }, clientIp: ClientIp);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Factory.Emails.Clear();
        (await client.PostJsonAsync(RouteConsts.Auth.ForgotPassword, new { email }, clientIp: ClientIp)).Dispose();
        string resetToken = AuthTestClient.ExtractToken(Assert.Single(Factory.Emails.Messages));
        using HttpResponseMessage reset = await client.PostJsonAsync(
            RouteConsts.Auth.ResetPassword,
            new { token = resetToken, newPassword = NewPassword },
            clientIp: ClientIp);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // Five wrong passwords lock the account.
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            (await client.LoginAsync(email, WrongPassword, deviceId, ClientIp, Chrome)).Dispose();
        }

        (SecurityEventType, SecurityEventOutcome, bool HasUser)[] expected =
        [
            (SecurityEventType.Login, SecurityEventOutcome.Failed, true),
            (SecurityEventType.Login, SecurityEventOutcome.Failed, false),
            (SecurityEventType.Login, SecurityEventOutcome.Succeeded, true),
            (SecurityEventType.RefreshTokenReuse, SecurityEventOutcome.Failed, true),
            (SecurityEventType.Login, SecurityEventOutcome.Succeeded, true),
            (SecurityEventType.Logout, SecurityEventOutcome.Succeeded, true),
            (SecurityEventType.Login, SecurityEventOutcome.Succeeded, true),
            (SecurityEventType.LogoutAllDevices, SecurityEventOutcome.Succeeded, true),
            (SecurityEventType.PasswordResetCompleted, SecurityEventOutcome.Succeeded, true),
            (SecurityEventType.Login, SecurityEventOutcome.Failed, true),
            (SecurityEventType.Login, SecurityEventOutcome.Failed, true),
            (SecurityEventType.Login, SecurityEventOutcome.Failed, true),
            (SecurityEventType.Login, SecurityEventOutcome.Failed, true),
            (SecurityEventType.Login, SecurityEventOutcome.Failed, true),
            (SecurityEventType.LockoutTriggered, SecurityEventOutcome.LockedOut, true),
        ];
        IReadOnlyList<AuditRecord> records = Factory.Audit.Records;
        Assert.Equal(expected, records.Select(r => (r.EventType, r.Outcome, r.UserId is not null)));
        Assert.Equal(records.Count, records.Select(r => r.EventId).Distinct().Count());

        foreach (AuditRecord record in records)
        {
            if (record.UserId is null)
            {
                Assert.Equal(Guid.Empty, record.KeyId);
                Assert.Empty(record.IpAddressCipher);
                Assert.Empty(record.DeviceTypeCipher);

                continue;
            }

            Assert.Equal(registeredSession.Account.Id, record.UserId);
            DecryptedAudit? decrypted = await CapturingAuditSink.DecryptAsync(record, Factory.Services);
            Assert.NotNull(decrypted);
            Assert.Equal(ClientIp, decrypted.IpAddress);
            Assert.False(string.IsNullOrWhiteSpace(decrypted.DeviceType));
        }

        // Logins carry the label of the browser that made them.
        foreach (AuditRecord login in records.Where(r => r.EventType == SecurityEventType.Login && r.UserId is not null))
        {
            DecryptedAudit? decrypted = await CapturingAuditSink.DecryptAsync(login, Factory.Services);
            Assert.Equal("Chrome, Windows, komputer", decrypted?.DeviceType);
        }

        // Nothing in the stream holds the e-mail, a password or a token in the clear.
        string stream = JsonSerializer.Serialize(records);
        string[] secrets =
        [
            email, email[..email.IndexOf('@', StringComparison.Ordinal)], Password, NewPassword, WrongPassword,
            registeredSession.AccessToken, session.AccessToken, secondSession.AccessToken, firstCookie.Value,
            rotatedAway, verificationToken, resetToken, ClientIp,
        ];
        Assert.All(secrets, secret => Assert.DoesNotContain(secret, stream, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_UnknownEmailLockout_WritesFiveFailedRecordsAndOneLockoutRecordNoneWithPersonalData()
    {
        string email = $"nikt-{Guid.NewGuid():N}@example.pl";
        using AuthTestClient client = Factory.CreateAuthClient();

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            (await client.LoginAsync(email, WrongPassword, clientIp: ClientIp, userAgent: Chrome)).Dispose();
        }

        IReadOnlyList<AuditRecord> records = Factory.Audit.Records;
        Assert.Equal(5, records.Count(r => r.EventType == SecurityEventType.Login && r.Outcome == SecurityEventOutcome.Failed));
        Assert.Single(records, r => r.EventType == SecurityEventType.LockoutTriggered);
        Assert.Equal(6, records.Count);
        Assert.All(records, r =>
        {
            Assert.Null(r.UserId);
            Assert.Equal(Guid.Empty, r.KeyId);
            Assert.Empty(r.IpAddressCipher);
            Assert.Empty(r.DeviceTypeCipher);
        });
        string stream = JsonSerializer.Serialize(records);
        Assert.DoesNotContain(email, stream, StringComparison.Ordinal);
        Assert.DoesNotContain(ClientIp, stream, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_RoutineRotation_WritesNoRecord()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (HttpResponseMessage registered, _) = await client.RegisterAsync(clientIp: ClientIp);
        registered.Dispose();

        for (int i = 0; i < 3; i++)
        {
            using HttpResponseMessage response = await client.RefreshAsync(ClientIp);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Empty(Factory.Audit.Records);
    }
}
