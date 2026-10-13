using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Api.IntegrationTests.Infrastructure.Fakers;
using Liftingo.Infrastructure.Email;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;

using Microsoft.EntityFrameworkCore;

using Serilog.Events;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class RegisterTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Register_ValidRequest_Returns201WithSessionAndCookie()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        (HttpResponseMessage response, RegisterRequestBody request) = await client.RegisterAsync();
        using HttpResponseMessage owned = response;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);
        Assert.False(string.IsNullOrEmpty(session.AccessToken));
        Assert.Equal(request.Email, session.Account.Email);
        Assert.False(session.Account.EmailVerified);
        Assert.True(session.Account.HasPassword);
        Assert.Empty(session.Account.Providers);

        RefreshCookie? cookie = AuthTestClient.ReadRefreshCookie(response);
        Assert.NotNull(cookie);
        Assert.False(string.IsNullOrEmpty(cookie.Value));
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal("Lax", cookie.SameSite, ignoreCase: true);
        Assert.Equal("/api/v1/auth/session", cookie.Path);
        Assert.InRange(cookie.MaxAgeSeconds ?? long.MaxValue, 1, 2_592_000);

        // The refresh token is only in the cookie, never in the body.
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(cookie.Value, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_ValidRequest_StoresThreeConsentsWithDocumentVersion()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        RegisterRequestBody request = new RegisterRequestFaker().Generate() with { AcceptMarketing = false };

        using HttpResponseMessage response = await client.RegisterAsync(request);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);

        List<UserConsent> consents = await WithDbAsync(db =>
            db.UserConsents.Where(c => c.UserId == session.Account.Id).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, consents.Count);
        Assert.All(consents, c => Assert.Equal(ApiFactory.LegalDocumentsVersion, c.DocumentVersion));
        Assert.All(consents, c => Assert.Null(c.SupersededAt));
        Assert.True(consents.Single(c => c.Type == ConsentType.TermsAndAge).Granted);
        Assert.True(consents.Single(c => c.Type == ConsentType.HealthData).Granted);
        Assert.False(consents.Single(c => c.Type == ConsentType.Marketing).Granted);
    }

    [Fact]
    public async Task Register_MarketingAccepted_StoresGrantedMarketingConsent()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        RegisterRequestBody request = new RegisterRequestFaker().Generate() with { AcceptMarketing = true };

        using HttpResponseMessage response = await client.RegisterAsync(request);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);

        UserConsent marketing = await WithDbAsync(db => db.UserConsents
            .SingleAsync(c => c.UserId == session.Account.Id && c.Type == ConsentType.Marketing, TestContext.Current.CancellationToken));
        Assert.True(marketing.Granted);
    }

    [Fact]
    public async Task Register_ValidRequest_SendsOneVerificationAndWelcomeEmailAndStoresOneDevice()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        Guid deviceId = Guid.NewGuid();

        (HttpResponseMessage response, RegisterRequestBody request) = await client.RegisterAsync(deviceId: deviceId);
        using HttpResponseMessage owned = response;
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);

        // Exactly one e-mail: verification + welcome. A registration never sends the new-device e-mail.
        EmailMessage email = Assert.Single(Factory.Emails.Messages);
        Assert.Equal(AccountEmailRenderer.VerificationAndWelcomeTemplate, email.TemplateName);
        Assert.Equal(request.Email, email.To);
        Assert.Equal(session.Account.Id, email.UserId);

        string token = AuthTestClient.ExtractToken(email);
        byte[] hash = OpaqueToken.Hash(token)!;
        EmailToken row = await WithDbAsync(db => db.EmailTokens
            .SingleAsync(t => t.TokenHash == hash, TestContext.Current.CancellationToken));
        Assert.Equal(EmailTokenPurpose.EmailVerification, row.Purpose);
        Assert.Equal(session.Account.Id, row.UserId);
        Assert.True(row.IsUsable(Factory.Time.GetUtcNow()));

        KnownDevice device = await WithDbAsync(db => db.KnownDevices
            .SingleAsync(d => d.UserId == session.Account.Id, TestContext.Current.CancellationToken));
        Assert.Equal(deviceId, device.DeviceId);
    }

    [Fact]
    public async Task Register_AddressWithSurroundingWhitespace_StoresTrimmedEmail()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        RegisterRequestBody request = new RegisterRequestFaker().Generate();

        using HttpResponseMessage response = await client.RegisterAsync(request with { Email = $"  {request.Email}  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);
        Assert.Equal(request.Email, session.Account.Email);
        Assert.Equal(request.Email, Assert.Single(Factory.Emails.Messages).To);
    }

    [Theory]
    [InlineData(false, true, "Validation.TermsRequired")]
    [InlineData(true, false, "Validation.HealthDataRequired")]
    public async Task Register_RequiredConsentMissing_Returns400AndCreatesNoUser(
        bool acceptTermsAndAge,
        bool acceptHealthData,
        string expectedCode)
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        RegisterRequestBody request = new RegisterRequestFaker().Generate() with
        {
            AcceptTermsAndAge = acceptTermsAndAge,
            AcceptHealthData = acceptHealthData,
        };

        using HttpResponseMessage response = await client.RegisterAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedCode, await ErrorCodesAsync(response));
        Assert.Equal(0, await WithDbAsync(db => db.Users.CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(0, await WithDbAsync(db => db.UserConsents.CountAsync(TestContext.Current.CancellationToken)));
        Assert.Empty(Factory.Emails.Messages);
    }

    [Fact]
    public async Task Register_PasswordWithoutDigitAndSpecial_Returns400WithBothPolicyCodes()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        (HttpResponseMessage response, _) = await client.RegisterAsync(password: "abcdefgh");
        using HttpResponseMessage owned = response;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        IReadOnlyList<string> codes = await ErrorCodesAsync(response);
        Assert.Contains("Validation.PasswordRequiresDigit", codes);
        Assert.Contains("Validation.PasswordRequiresSpecial", codes);
        Assert.Equal(0, await WithDbAsync(db => db.Users.CountAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400WithPasswordTooShort()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        (HttpResponseMessage response, _) = await client.RegisterAsync(password: "Ab1!");
        using HttpResponseMessage owned = response;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Validation.PasswordTooShort", await ErrorCodesAsync(response));
    }

    [Fact]
    public async Task Register_PasswordLongerThan128Chars_Returns400WithPasswordTooLong()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        (HttpResponseMessage response, _) = await client.RegisterAsync(password: new string('a', 127) + "1!");
        using HttpResponseMessage owned = response;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Validation.PasswordTooLong", await ErrorCodesAsync(response));
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400WithEmailInvalid()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        (HttpResponseMessage response, _) = await client.RegisterAsync(email: "not-an-email");
        using HttpResponseMessage owned = response;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Validation.EmailInvalid", await ErrorCodesAsync(response));
    }

    [Fact]
    public async Task Register_ExistingEmailInDifferentCase_Returns409EmailTaken()
    {
        using AuthTestClient first = Factory.CreateAuthClient();
        (HttpResponseMessage created, RegisterRequestBody original) = await first.RegisterAsync();
        created.Dispose();
        Factory.Emails.Clear();
        using AuthTestClient second = Factory.CreateAuthClient();

        using HttpResponseMessage response = await second.RegisterAsync(original with { Email = original.Email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Auth.EmailTaken", body.GetProperty("code").GetString());
        Assert.Null(AuthTestClient.ReadRefreshCookie(response));
        Assert.Empty(Factory.Emails.Messages);
        Assert.Equal(1, await WithDbAsync(db => db.Users.CountAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Register_TwoParallelRequestsForOneAddress_OneSucceedsAndOneGetsEmailTaken()
    {
        using AuthTestClient first = Factory.CreateAuthClient();
        using AuthTestClient second = Factory.CreateAuthClient();
        RegisterRequestBody request = new RegisterRequestFaker().Generate();

        HttpResponseMessage[] responses = await Task.WhenAll(first.RegisterAsync(request), second.RegisterAsync(request));

        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, await WithDbAsync(db => db.Users.CountAsync(TestContext.Current.CancellationToken)));
            Assert.Equal(3, await WithDbAsync(db => db.UserConsents.CountAsync(TestContext.Current.CancellationToken)));
            Assert.Single(Factory.Emails.Messages);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Register_EmailSenderThrows_StillReturns201AndLogsTemplateAndUserIdOnly()
    {
        // A host of its own: Serilog's bootstrap logger is process-wide and follows the newest host, so only
        // a freshly created factory reliably receives the log events of its requests.
        using ApiFactory factory = new(Fixture.ConnectionString, Fixture.RedisConnectionString);
        factory.Emails.ThrowOnSend = true;
        using AuthTestClient client = factory.CreateAuthClient();

        (HttpResponseMessage response, RegisterRequestBody request) = await client.RegisterAsync();
        using HttpResponseMessage owned = response;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(response);

        LogEvent failure = Assert.Single(
            factory.Logs.Events,
            e => PropertyValue(e, "TemplateName") is string name && name == AccountEmailRenderer.VerificationAndWelcomeTemplate);
        Assert.Equal(session.Account.Id, PropertyValue(failure, "UserId"));
        Assert.DoesNotContain(
            factory.Logs.Events,
            e => e.RenderMessage(null).Contains(request.Email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_ValidRequest_AccessTokenWorksOnGetCurrentAccount()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (HttpResponseMessage registered, _) = await client.RegisterAsync();
        using HttpResponseMessage registeredResponse = registered;
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(registeredResponse);

        using HttpResponseMessage me = await client.GetMeAsync(session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    private static object? PropertyValue(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out LogEventPropertyValue? value) && value is ScalarValue scalar
            ? scalar.Value
            : null;

    private static async Task<IReadOnlyList<string>> ErrorCodesAsync(HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return [.. body.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("code").GetString()!)];
    }
}
