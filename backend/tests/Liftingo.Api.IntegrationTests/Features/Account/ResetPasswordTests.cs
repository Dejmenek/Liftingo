using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Audit;
using Liftingo.Infrastructure.Identity;
using Liftingo.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class ResetPasswordTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string OldPassword = "Sztanga1!";
    private const string NewPassword = "Nowe1haslo!";

    [Fact]
    public async Task Reset_ValidTokenAndPassword_Returns204WithoutASessionAndSwapsThePassword()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await RegisterVerifiedAsync(client, Factory);
        string token = await RequestResetTokenAsync(client, Factory, session.Account.Email);

        using HttpResponseMessage response = await ResetAsync(client, token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // No session starts: no cookie with a value (a cookie that only clears an old one is fine).
        RefreshCookie? cookie = AuthTestClient.ReadRefreshCookie(response);
        Assert.True(cookie is null || cookie.IsDeleted);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        using HttpResponseMessage oldPassword = await client.LoginAsync(session.Account.Email, OldPassword);
        using HttpResponseMessage newPassword = await client.LoginAsync(session.Account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Fact]
    public async Task Reset_EveryEarlierSession_IsRevokedIncludingTheCallers()
    {
        using AuthTestClient callerDevice = Factory.CreateAuthClient();
        (AuthSessionResponse callerSession, _) = await RegisterVerifiedAsync(callerDevice, Factory);
        using AuthTestClient otherDevice = Factory.CreateAuthClient();
        using HttpResponseMessage otherLogin = await otherDevice.LoginAsync(callerSession.Account.Email, OldPassword, Guid.NewGuid());
        AuthSessionResponse otherSession = await AuthTestClient.ReadSessionAsync(otherLogin);
        string token = await RequestResetTokenAsync(callerDevice, Factory, callerSession.Account.Email);

        // The caller still holds its refresh cookie while resetting.
        (await ResetAsync(callerDevice, token)).Dispose();

        foreach (AuthTestClient device in new[] { callerDevice, otherDevice })
        {
            using HttpResponseMessage refresh = await device.RefreshAsync();
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }

        foreach (AuthSessionResponse session in new[] { callerSession, otherSession })
        {
            using HttpResponseMessage me = await callerDevice.GetMeAsync(session.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }
    }

    [Fact]
    public async Task Reset_Completed_DeletesKnownDevicesAndWritesAuditRecord()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await RegisterVerifiedAsync(client, Factory);
        Guid userId = session.Account.Id;
        Assert.Equal(1, await WithDbAsync(db => db.KnownDevices.CountAsync(d => d.UserId == userId, TestContext.Current.CancellationToken)));
        string token = await RequestResetTokenAsync(client, Factory, session.Account.Email);
        Factory.Audit.Clear();

        (await ResetAsync(client, token)).Dispose();

        Assert.Equal(0, await WithDbAsync(db => db.KnownDevices.CountAsync(d => d.UserId == userId, TestContext.Current.CancellationToken)));
        AuditRecord record = Assert.Single(Factory.Audit.Records);
        Assert.Equal(SecurityEventType.PasswordResetCompleted, record.EventType);
        Assert.Equal(SecurityEventOutcome.Succeeded, record.Outcome);
        Assert.Equal(userId, record.UserId);
    }

    [Fact]
    public async Task Reset_UsedToken_Returns400ResetLinkInvalid()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await RegisterVerifiedAsync(client, Factory);
        string token = await RequestResetTokenAsync(client, Factory, session.Account.Email);
        (await ResetAsync(client, token)).Dispose();

        using HttpResponseMessage again = await ResetAsync(client, token, "Jeszcze1inne!");

        await AssertErrorAsync(again, "Auth.ResetLinkInvalid");
        using HttpResponseMessage login = await client.LoginAsync(session.Account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Reset_TokenOlderThanOneHour_Returns400ResetLinkInvalid()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        using AuthTestClient client = factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await RegisterVerifiedAsync(client, factory);
        string token = await RequestResetTokenAsync(client, factory, session.Account.Email);
        factory.Time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));

        using HttpResponseMessage response = await ResetAsync(client, token);

        await AssertErrorAsync(response, "Auth.ResetLinkInvalid");
    }

    [Fact]
    public async Task Reset_SupersededToken_Returns400ResetLinkInvalid()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await RegisterVerifiedAsync(client, Factory);
        string first = await RequestResetTokenAsync(client, Factory, session.Account.Email);
        _ = await RequestResetTokenAsync(client, Factory, session.Account.Email);

        using HttpResponseMessage response = await ResetAsync(client, first);

        await AssertErrorAsync(response, "Auth.ResetLinkInvalid");
    }

    [Theory]
    [InlineData("not a token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Reset_UnknownOrMalformedToken_Returns400ResetLinkInvalid(string token)
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await ResetAsync(client, token);

        await AssertErrorAsync(response, "Auth.ResetLinkInvalid");
    }

    [Fact]
    public async Task Reset_TokenOfAnotherPurpose_Returns400ResetLinkInvalid()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (_, string verificationToken) = await VerifyEmailTests.RegisterAsync(client, Factory);

        using HttpResponseMessage response = await ResetAsync(client, verificationToken);

        await AssertErrorAsync(response, "Auth.ResetLinkInvalid");
    }

    [Fact]
    public async Task Reset_PasswordBreakingThePolicy_Returns400AndLeavesTheTokenUsable()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await RegisterVerifiedAsync(client, Factory);
        string token = await RequestResetTokenAsync(client, Factory, session.Account.Email);

        using HttpResponseMessage weak = await ResetAsync(client, token, "abcdefgh");

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        JsonElement body = await weak.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        string[] codes = [.. body.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("code").GetString()!)];
        Assert.Contains("Validation.PasswordRequiresDigit", codes);
        Assert.Contains("Validation.PasswordRequiresSpecial", codes);

        using HttpResponseMessage oldStillWorks = await client.LoginAsync(session.Account.Email, OldPassword);
        Assert.Equal(HttpStatusCode.OK, oldStillWorks.StatusCode);

        using HttpResponseMessage retry = await ResetAsync(client, token);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact]
    public async Task Reset_LockedAccount_WorksAndClearsLockoutAndFailedCount()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await RegisterVerifiedAsync(client, Factory);
        Guid userId = session.Account.Id;

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            (await client.LoginAsync(session.Account.Email, "Zle-haslo1!")).Dispose();
        }

        using HttpResponseMessage locked = await client.LoginAsync(session.Account.Email, OldPassword);
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        string token = await RequestResetTokenAsync(client, Factory, session.Account.Email);

        using HttpResponseMessage response = await ResetAsync(client, token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        ApplicationUser user = await WithDbAsync(db => db.Users
            .AsNoTracking()
            .SingleAsync(u => u.Id == userId, TestContext.Current.CancellationToken));
        Assert.Null(user.LockoutEnd);
        Assert.Equal(0, user.AccessFailedCount);
        using HttpResponseMessage login = await client.LoginAsync(session.Account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Reset_ProviderOnlyAccount_CanLogInWithEmailAndPasswordAfterwards()
    {
        string email = await CreateProviderOnlyUserAsync();
        using AuthTestClient client = Factory.CreateAuthClient();
        string token = await RequestResetTokenAsync(client, Factory, email);

        using HttpResponseMessage response = await ResetAsync(client, token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using HttpResponseMessage login = await client.LoginAsync(email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(login);
        Assert.True(session.Account.HasPassword);
    }

    [Fact]
    public async Task Reset_UnverifiedAccountWithoutConsents_Returns400ConsentRequiredAndChangesNothing()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await VerifyEmailTests.RegisterAsync(client, Factory);
        string token = await RequestResetTokenAsync(client, Factory, session.Account.Email);

        using HttpResponseMessage noConsents = await ResetAsync(client, token);
        using HttpResponseMessage oneConsent = await ResetAsync(client, token, acceptTermsAndAge: true, acceptHealthData: false);

        await AssertErrorAsync(noConsents, "Auth.ConsentRequired");
        await AssertErrorAsync(oneConsent, "Auth.ConsentRequired");
        using HttpResponseMessage oldStillWorks = await client.LoginAsync(session.Account.Email, "WrongPass1!");
        Assert.Equal(HttpStatusCode.Unauthorized, oldStillWorks.StatusCode);
        Assert.False(await EmailConfirmedAsync(session.Account.Id));

        // The token is still usable: the same reset with both consents completes.
        using HttpResponseMessage withConsents = await ResetAsync(client, token, acceptTermsAndAge: true, acceptHealthData: true);
        Assert.Equal(HttpStatusCode.NoContent, withConsents.StatusCode);
    }

    [Fact]
    public async Task Reset_UnverifiedAccountWithBothConsents_TakesTheAccountOver()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await VerifyEmailTests.RegisterAsync(client, Factory);
        Guid userId = session.Account.Id;
        string token = await RequestResetTokenAsync(client, Factory, session.Account.Email);

        using HttpResponseMessage response = await ResetAsync(
            client,
            token,
            acceptTermsAndAge: true,
            acceptHealthData: true,
            acceptMarketing: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await EmailConfirmedAsync(userId));

        List<UserConsent> consents = await WithDbAsync(db => db.UserConsents
            .Where(c => c.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, consents.Count);
        Assert.Equal(3, consents.Count(c => c.SupersededAt is not null));
        List<UserConsent> current = [.. consents.Where(c => c.SupersededAt is null)];
        Assert.Equal(3, current.Count);
        Assert.All(current.Where(c => c.Type != ConsentType.Marketing), c => Assert.True(c.Granted));
        Assert.True(current.Single(c => c.Type == ConsentType.Marketing).Granted);

        List<EmailToken> tokens = await WithDbAsync(db => db.EmailTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.All(tokens, t => Assert.False(t.IsUsable(Factory.Time.GetUtcNow())));
        Assert.Contains(tokens, t => t.Purpose == EmailTokenPurpose.EmailVerification && t.SupersededAt is not null);

        // Every earlier session is gone and the new password works.
        using HttpResponseMessage refresh = await client.RefreshAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using HttpResponseMessage login = await client.LoginAsync(session.Account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Reset_VerifiedAccount_IgnoresTheConsentFields()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (AuthSessionResponse session, _) = await RegisterVerifiedAsync(client, Factory);
        Guid userId = session.Account.Id;
        string token = await RequestResetTokenAsync(client, Factory, session.Account.Email);

        using HttpResponseMessage response = await ResetAsync(
            client,
            token,
            acceptTermsAndAge: false,
            acceptHealthData: false,
            acceptMarketing: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        List<UserConsent> consents = await WithDbAsync(db => db.UserConsents
            .Where(c => c.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, consents.Count);
        Assert.All(consents, c => Assert.Null(c.SupersededAt));
        Assert.False(consents.Single(c => c.Type == ConsentType.Marketing).Granted);
    }

    [Fact]
    public async Task Reset_EmptyToken_Returns400Validation()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        const string code = "Validation.TokenRequired";

        using HttpResponseMessage response = await ResetAsync(client, string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), e => e.GetProperty("code").GetString() == code);
    }

    /// <summary>Registers an account and verifies its e-mail, so a reset does not need consents.</summary>
    private static async Task<(AuthSessionResponse Session, string Token)> RegisterVerifiedAsync(AuthTestClient client, ApiFactory factory)
    {
        (AuthSessionResponse session, string token) = await VerifyEmailTests.RegisterAsync(client, factory);
        using HttpResponseMessage verified = await client.PostJsonAsync(RouteConsts.Auth.VerifyEmail, new { token });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        return (session, token);
    }

    private static async Task<string> RequestResetTokenAsync(AuthTestClient client, ApiFactory factory, string email)
    {
        factory.Emails.Clear();
        using HttpResponseMessage forgot = await client.PostJsonAsync(RouteConsts.Auth.ForgotPassword, new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);

        return AuthTestClient.ExtractToken(Assert.Single(factory.Emails.Messages));
    }

    private static Task<HttpResponseMessage> ResetAsync(
        AuthTestClient client,
        string token,
        string newPassword = NewPassword,
        bool? acceptTermsAndAge = null,
        bool? acceptHealthData = null,
        bool? acceptMarketing = null) =>
        client.PostJsonAsync(
            RouteConsts.Auth.ResetPassword,
            new { token, newPassword, acceptTermsAndAge, acceptHealthData, acceptMarketing });

    private static async Task AssertErrorAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    private Task<bool> EmailConfirmedAsync(Guid userId) =>
        WithDbAsync(db => db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.EmailConfirmed)
            .SingleAsync(TestContext.Current.CancellationToken));

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
}
