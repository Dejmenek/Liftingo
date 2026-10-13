using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Api.IntegrationTests.Infrastructure.Fakers;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Audit;
using Liftingo.Infrastructure.Identity;
using Liftingo.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class LoginTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ChromeOnWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

    private const string WrongPassword = "Zle-haslo1!";

    [Fact]
    public async Task Login_CorrectCredentials_Returns200WithSessionAndCookie()
    {
        (string email, string password, Guid userId) = await RegisterUserAsync();
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);
        Assert.False(string.IsNullOrEmpty(session.AccessToken));
        Assert.Equal(userId, session.Account.Id);
        Assert.Equal(email, session.Account.Email);
        Assert.True(session.Account.HasPassword);

        RefreshCookie? cookie = AuthTestClient.ReadRefreshCookie(response);
        Assert.NotNull(cookie);
        Assert.False(string.IsNullOrEmpty(cookie.Value));
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal("Lax", cookie.SameSite, ignoreCase: true);
        Assert.Equal("/api/v1/auth/session", cookie.Path);
        Assert.InRange(cookie.MaxAgeSeconds ?? long.MaxValue, 1, 2_592_000);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(cookie.Value, body, StringComparison.Ordinal);

        using HttpResponseMessage me = await client.GetMeAsync(session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_EmailWithWhitespaceInDifferentCase_Succeeds()
    {
        (string email, string password, _) = await RegisterUserAsync();
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.LoginAsync($"  {email.ToUpperInvariant()}  ", password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Sztanga1!", "Validation.EmailRequired")]
    [InlineData("anna@example.pl", "", "Validation.PasswordRequired")]
    public async Task Login_EmptyField_Returns400(string email, string password, string expectedCode)
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Contains(
            body.GetProperty("errors").EnumerateArray(),
            e => e.GetProperty("code").GetString() == expectedCode);
    }

    [Fact]
    public async Task Login_WrongPasswordUnknownEmailAndProviderOnlyAccount_ReturnIdenticalInvalidCredentialsBodies()
    {
        (string email, _, _) = await RegisterUserAsync();
        string providerOnlyEmail = await CreateProviderOnlyUserAsync();
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage wrongPassword = await client.LoginAsync(email, WrongPassword);
        using HttpResponseMessage unknownEmail = await client.LoginAsync("nikt@example.pl", WrongPassword);
        using HttpResponseMessage providerOnly = await client.LoginAsync(providerOnlyEmail, WrongPassword);

        foreach (HttpResponseMessage response in new[] { wrongPassword, unknownEmail, providerOnly })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(AuthTestClient.ReadRefreshCookie(response));
        }

        string reference = await StableBodyAsync(wrongPassword);
        Assert.Contains("Auth.InvalidCredentials", reference, StringComparison.Ordinal);
        Assert.Equal(reference, await StableBodyAsync(unknownEmail));
        Assert.Equal(reference, await StableBodyAsync(providerOnly));
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_FifthAttemptIsLockedOutWithRetryAfter()
    {
        (string email, string password, _) = await RegisterUserAsync();
        using AuthTestClient client = Factory.CreateAuthClient();

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            using HttpResponseMessage failed = await client.LoginAsync(email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using HttpResponseMessage locked = await client.LoginAsync(email, WrongPassword);

        string firstRetryAfter = await AssertLockedOutAsync(locked);

        // The right password is refused as well, and the refusal does not move the retry time.
        using HttpResponseMessage rightPassword = await client.LoginAsync(email, password);
        Assert.Equal(firstRetryAfter, await AssertLockedOutAsync(rightPassword));
        Assert.Null(AuthTestClient.ReadRefreshCookie(rightPassword));
    }

    [Fact]
    public async Task Login_LockoutEndMovedIntoThePast_SucceedsAndResetsFailedCount()
    {
        (string email, string password, Guid userId) = await RegisterUserAsync();
        using AuthTestClient client = Factory.CreateAuthClient();

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            (await client.LoginAsync(email, WrongPassword)).Dispose();
        }

        // Identity's lockout uses the system clock, so the test ends the lockout in the database.
        await WithDbAsync(db => db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.LockoutEnd, DateTimeOffset.UtcNow.AddMinutes(-1)),
                TestContext.Current.CancellationToken));

        using HttpResponseMessage response = await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await FailedCountAsync(userId));
    }

    [Fact]
    public async Task Login_SuccessAfterSomeFailures_ResetsFailedCount()
    {
        (string email, string password, Guid userId) = await RegisterUserAsync();
        using AuthTestClient client = Factory.CreateAuthClient();
        (await client.LoginAsync(email, WrongPassword)).Dispose();
        (await client.LoginAsync(email, WrongPassword)).Dispose();
        Assert.Equal(2, await FailedCountAsync(userId));

        using HttpResponseMessage response = await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await FailedCountAsync(userId));
    }

    [Fact]
    public async Task Login_UnknownEmailFiveTimes_ReturnsTheSameLockoutAndItEndsWithTheFakeClock()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        using AuthTestClient client = factory.CreateAuthClient();
        const string unknown = "nikt@example.pl";

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            using HttpResponseMessage failed = await client.LoginAsync(unknown, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using HttpResponseMessage locked = await client.LoginAsync(unknown, WrongPassword);
        string retryAfter = await AssertLockedOutAsync(locked);

        // Another spelling of the same address is the same throttle entry.
        using HttpResponseMessage stillLocked = await client.LoginAsync($" {unknown.ToUpperInvariant()} ", WrongPassword);
        Assert.Equal(retryAfter, await AssertLockedOutAsync(stillLocked));

        factory.Time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        using HttpResponseMessage afterExpiry = await client.LoginAsync(unknown, WrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, afterExpiry.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmailLockedOutOnOneInstance_IsStillLockedOnAnotherWithTheSameRetryTime()
    {
        const string unknown = "nikt-nigdzie@example.pl";
        string retryAfter;

        using (ApiFactory first = CreateIsolatedFactory())
        using (AuthTestClient client = first.CreateAuthClient())
        {
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                (await client.LoginAsync(unknown, WrongPassword)).Dispose();
            }

            using HttpResponseMessage locked = await client.LoginAsync(unknown, WrongPassword);
            retryAfter = await AssertLockedOutAsync(locked);
        }

        // A restart, or a second instance: a new host on the same Redis (FR-013).
        using ApiFactory second = CreateIsolatedFactory();
        using AuthTestClient other = second.CreateAuthClient();

        using HttpResponseMessage stillLocked = await other.LoginAsync(unknown, WrongPassword);

        Assert.Equal(retryAfter, await AssertLockedOutAsync(stillLocked));
    }

    [Fact]
    public async Task Login_KnownUserAttempts_WriteOneLoginRecordEachDecryptingToIpAndDevice()
    {
        (string email, string password, Guid userId) = await RegisterUserAsync();
        using AuthTestClient client = Factory.CreateAuthClient();

        (await client.LoginAsync(email, WrongPassword, clientIp: "203.0.113.5", userAgent: ChromeOnWindows)).Dispose();
        (await client.LoginAsync(email, password, clientIp: "203.0.113.5", userAgent: ChromeOnWindows)).Dispose();

        List<AuditRecord> records = [.. Factory.Audit.Records.Where(r => r.EventType == SecurityEventType.Login)];
        Assert.Equal(2, records.Count);
        Assert.Equal(
            [SecurityEventOutcome.Failed, SecurityEventOutcome.Succeeded],
            records.Select(r => r.Outcome));
        Assert.All(records, r => Assert.Equal(userId, r.UserId));

        foreach (AuditRecord record in records)
        {
            DecryptedAudit? decrypted = await CapturingAuditSink.DecryptAsync(record, Factory.Services);
            Assert.NotNull(decrypted);
            Assert.Equal("203.0.113.5", decrypted.IpAddress);
            Assert.Equal("Chrome, Windows, komputer", decrypted.DeviceType);
        }
    }

    [Fact]
    public async Task Login_FifthWrongPassword_AlsoWritesLockoutTriggeredRecord()
    {
        (string email, _, Guid userId) = await RegisterUserAsync();
        using AuthTestClient client = Factory.CreateAuthClient();

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            (await client.LoginAsync(email, WrongPassword)).Dispose();
        }

        Assert.Equal(5, Factory.Audit.Records.Count(r => r.EventType == SecurityEventType.Login));
        Assert.Equal(5, Factory.Audit.Records.Count(r => r.EventType == SecurityEventType.Login && r.Outcome == SecurityEventOutcome.Failed));
        AuditRecord triggered = Assert.Single(Factory.Audit.Records, r => r.EventType == SecurityEventType.LockoutTriggered);
        Assert.Equal(userId, triggered.UserId);

        // An attempt during the lockout is audited as refused.
        (await client.LoginAsync(email, WrongPassword)).Dispose();
        Assert.Contains(
            Factory.Audit.Records,
            r => r.EventType == SecurityEventType.Login && r.Outcome == SecurityEventOutcome.LockedOut && r.UserId == userId);
        Assert.Single(Factory.Audit.Records, r => r.EventType == SecurityEventType.LockoutTriggered);
    }

    [Fact]
    public async Task Login_UnknownEmail_AuditRecordHasNoUserKeyIpOrDevice()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        (await client.LoginAsync("nikt@example.pl", WrongPassword, clientIp: "203.0.113.9", userAgent: ChromeOnWindows)).Dispose();

        AuditRecord record = Assert.Single(Factory.Audit.Records);
        Assert.Equal(SecurityEventType.Login, record.EventType);
        Assert.Equal(SecurityEventOutcome.Failed, record.Outcome);
        Assert.Null(record.UserId);
        Assert.Equal(Guid.Empty, record.KeyId);
        Assert.Empty(record.IpAddressCipher);
        Assert.Empty(record.DeviceTypeCipher);
    }

    [Fact]
    public async Task Login_UnknownEmailLockout_WritesLockoutTriggeredRecordWithoutUser()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            (await client.LoginAsync("nikt@example.pl", WrongPassword)).Dispose();
        }

        AuditRecord triggered = Assert.Single(Factory.Audit.Records, r => r.EventType == SecurityEventType.LockoutTriggered);
        Assert.Null(triggered.UserId);
        Assert.Equal(Guid.Empty, triggered.KeyId);
    }

    [Fact]
    public async Task Login_KnownDeviceId_UpdatesLastSeenAtWithoutNewRow()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        Guid deviceId = Guid.NewGuid();
        (string email, string password, Guid userId) = await RegisterUserAsync(deviceId, factory);
        factory.Time.Advance(TimeSpan.FromHours(3));
        using AuthTestClient client = factory.CreateAuthClient();

        using HttpResponseMessage response = await client.LoginAsync(email, password, deviceId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        KnownDevice device = await WithDbAsync(db => db.KnownDevices
            .SingleAsync(d => d.UserId == userId, TestContext.Current.CancellationToken));
        Assert.Equal(deviceId, device.DeviceId);
        Assert.Equal(factory.Time.GetUtcNow(), device.LastSeenAt, TimeSpan.FromSeconds(1));
        Assert.True(device.LastSeenAt > device.FirstSeenAt);
    }

    [Fact]
    public async Task Login_UnknownDeviceId_InsertsKnownDeviceRow()
    {
        (string email, string password, Guid userId) = await RegisterUserAsync(Guid.NewGuid());
        Guid newDevice = Guid.NewGuid();
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.LoginAsync(email, password, newDevice);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<KnownDevice> devices = await WithDbAsync(db => db.KnownDevices
            .Where(d => d.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, devices.Count);
        Assert.Contains(devices, d => d.DeviceId == newDevice);
    }

    private async Task<(string Email, string Password, Guid UserId)> RegisterUserAsync(Guid? deviceId = null, ApiFactory? factory = null)
    {
        factory ??= Factory;
        using AuthTestClient client = factory.CreateAuthClient();
        (HttpResponseMessage response, RegisterRequestBody request) = await client.RegisterAsync(deviceId: deviceId);
        using HttpResponseMessage owned = response;
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);

        // Start the test proper without the registration's e-mail and audit records.
        factory.Emails.Clear();
        factory.Audit.Clear();

        return (request.Email, request.Password, session.Account.Id);
    }

    private async Task<string> CreateProviderOnlyUserAsync()
    {
        string email = $"provider-{Guid.NewGuid():N}@example.pl";

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        IdentityResult created = await users.CreateAsync(new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAt = Factory.Time.GetUtcNow(),
        });
        Assert.True(created.Succeeded);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return email;
    }

    private Task<int> FailedCountAsync(Guid userId) =>
        WithDbAsync(db => db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.AccessFailedCount)
            .SingleAsync(TestContext.Current.CancellationToken));

    /// <returns>The <c>retryAfter</c> value as sent.</returns>
    private static async Task<string> AssertLockedOutAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Auth.LockedOut", body.GetProperty("code").GetString());

        string retryAfterText = body.GetProperty("retryAfter").GetString()!;
        DateTimeOffset retryAfter = DateTimeOffset.Parse(retryAfterText, CultureInfo.InvariantCulture);
        Assert.InRange(retryAfter - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(16));

        Assert.True(response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? header));
        Assert.InRange(int.Parse(header.Single(), CultureInfo.InvariantCulture), 840, 960);

        return retryAfterText;
    }

    // The body without the per-request trace id.
    private static async Task<string> StableBodyAsync(HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return string.Join(
            "|",
            body.EnumerateObject()
                .Where(p => p.Name != "traceId")
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{p.Name}={p.Value}"));
    }
}
