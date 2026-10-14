using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Liftingo.Api.IntegrationTests.Features.Account;

public class GetCurrentAccountTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task GetCurrentAccount_ValidBearer_ReturnsAccountOfTheRegisteredUser()
    {
        using AuthTestClient client = Factory.CreateAuthClient();
        (HttpResponseMessage registered, var request) = await client.RegisterAsync();
        using HttpResponseMessage registeredResponse = registered;
        AuthSessionResponse session = await AuthTestClient.ReadSessionAsync(registeredResponse);

        using HttpResponseMessage response = await client.GetMeAsync(session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement account = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(session.Account.Id, account.GetProperty("id").GetGuid());
        Assert.Equal(request.Email, account.GetProperty("email").GetString());
        Assert.False(account.GetProperty("emailVerified").GetBoolean());
        Assert.True(account.GetProperty("hasPassword").GetBoolean());
        Assert.Equal(0, account.GetProperty("providers").GetArrayLength());
    }

    [Fact]
    public async Task GetCurrentAccount_NoBearer_Returns401()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.GetMeAsync(accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentAccount_GarbageBearer_Returns401()
    {
        using AuthTestClient client = Factory.CreateAuthClient();

        using HttpResponseMessage response = await client.GetMeAsync("not-a-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}