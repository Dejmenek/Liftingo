using System.Net;

using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;
using Liftingo.Domain.Account;
using Liftingo.Infrastructure.Audit;

using Microsoft.EntityFrameworkCore;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class LogoutAllDevicesTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Password = "Sztanga1!";

    [Fact]
    public async Task LogoutAll_ThreeSessions_Returns204AndRevokesEveryOneIncludingTheCallers()
    {
        Guid deviceA = Guid.NewGuid();
        using AuthTestClient clientA = Factory.CreateAuthClient();
        AuthSessionResponse sessionA = await RegisterAsync(clientA, deviceA);
        using AuthTestClient clientB = Factory.CreateAuthClient();
        using AuthTestClient clientC = Factory.CreateAuthClient();
        AuthSessionResponse sessionB = await LoginAsync(clientB, sessionA.Account.Email);
        AuthSessionResponse sessionC = await LoginAsync(clientC, sessionA.Account.Email);
        Factory.Audit.Clear();

        using HttpResponseMessage response = await clientB.LogoutAllAsync(sessionB.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        RefreshCookie? cleared = AuthTestClient.ReadRefreshCookie(response);
        Assert.NotNull(cleared);
        Assert.True(cleared.IsDeleted);

        foreach (AuthTestClient client in new[] { clientA, clientB, clientC })
        {
            using HttpResponseMessage refresh = await client.RefreshAsync();
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }

        foreach (AuthSessionResponse session in new[] { sessionA, sessionB, sessionC })
        {
            using HttpResponseMessage me = await clientA.GetMeAsync(session.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }

        AuditRecord record = Assert.Single(Factory.Audit.Records);
        Assert.Equal(SecurityEventType.LogoutAllDevices, record.EventType);
        Assert.Equal(SecurityEventOutcome.Succeeded, record.Outcome);
        Assert.Equal(sessionA.Account.Id, record.UserId);
    }

    [Fact]
    public async Task LogoutAll_KnownDevices_AreDeletedAndTheNextSignInFromThemInsertsAFreshRow()
    {
        using ApiFactory factory = CreateIsolatedFactory();
        Guid deviceA = Guid.NewGuid();
        using AuthTestClient clientA = factory.CreateAuthClient();
        AuthSessionResponse sessionA = await RegisterAsync(clientA, deviceA);
        Guid userId = sessionA.Account.Id;
        using AuthTestClient clientB = factory.CreateAuthClient();
        await LoginAsync(clientB, sessionA.Account.Email);
        Assert.Equal(2, await DeviceCountAsync(userId));

        (await clientA.LogoutAllAsync(sessionA.AccessToken)).Dispose();

        Assert.Equal(0, await DeviceCountAsync(userId));

        factory.Time.Advance(TimeSpan.FromHours(1));
        using AuthTestClient again = factory.CreateAuthClient();
        using HttpResponseMessage login = await again.LoginAsync(sessionA.Account.Email, Password, deviceA);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        KnownDevice device = await WithDbAsync(db => db.KnownDevices
            .SingleAsync(d => d.UserId == userId, TestContext.Current.CancellationToken));
        Assert.Equal(deviceA, device.DeviceId);

        // It is a new row, not the old one coming back: it is first seen now.
        Assert.Equal(factory.Time.GetUtcNow(), device.FirstSeenAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task LogoutAll_OtherUsers_KeepTheirSessionsAndDevices()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        AuthSessionResponse session = await RegisterAsync(client, Guid.NewGuid());
        using AuthTestClient other = Factory.CreateAuthClient();
        AuthSessionResponse otherSession = await RegisterAsync(other, Guid.NewGuid());

        (await client.LogoutAllAsync(session.AccessToken)).Dispose();

        using HttpResponseMessage refresh = await other.RefreshAsync();
        using HttpResponseMessage me = await other.GetMeAsync(otherSession.AccessToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(1, await DeviceCountAsync(otherSession.Account.Id));
    }

    [Fact]
    public async Task LogoutAll_NoBearer_Returns401()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.LogoutAllAsync(accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(Factory.Audit.Records);
    }

    private Task<int> DeviceCountAsync(Guid userId) =>
        WithDbAsync(db => db.KnownDevices.CountAsync(d => d.UserId == userId, TestContext.Current.CancellationToken));

    private static async Task<AuthSessionResponse> LoginAsync(AuthTestClient client, string email)
    {
        using HttpResponseMessage response = await client.LoginAsync(email, Password, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await AuthTestClient.ReadSessionAsync(response);
    }

    private static async Task<AuthSessionResponse> RegisterAsync(AuthTestClient client, Guid deviceId)
    {
        (HttpResponseMessage response, _) = await client.RegisterAsync(password: Password, deviceId: deviceId);
        using HttpResponseMessage owned = response;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await AuthTestClient.ReadSessionAsync(response);
    }
}
