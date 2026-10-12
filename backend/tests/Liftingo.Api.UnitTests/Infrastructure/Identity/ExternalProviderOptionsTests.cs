using System.Security.Claims;

using AspNet.Security.OAuth.Twitter;

using Liftingo.Application.Common.Security;
using Liftingo.Infrastructure.Identity;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Liftingo.Api.UnitTests.Infrastructure.Identity;

public class ExternalProviderOptionsTests
{
    private static readonly Dictionary<string, string?> s_allProviders = new()
    {
        ["Authentication:Google:ClientId"] = "google-id",
        ["Authentication:Google:ClientSecret"] = "google-secret",
        ["Authentication:Facebook:AppId"] = "facebook-id",
        ["Authentication:Facebook:AppSecret"] = "facebook-secret",
        ["Authentication:X:ClientId"] = "x-id",
        ["Authentication:X:ClientSecret"] = "x-secret",
    };

    [Fact]
    public void AddExternalProviders_XConfigured_RequestsTheEmailScopeAndFieldAndMapsTheClaim()
    {
        using ServiceProvider provider = Build(s_allProviders);

        TwitterAuthenticationOptions options = provider.GetRequiredService<IOptionsMonitor<TwitterAuthenticationOptions>>().Get("X");

        Assert.Contains("users.email", options.Scope);
        Assert.Contains("confirmed_email", options.UserFields);
        JsonSubKeyClaimAction email = Assert.Single(
            options.ClaimActions.OfType<JsonSubKeyClaimAction>(),
            action => action.ClaimType == ClaimTypes.Email);
        Assert.Equal("data", email.JsonKey);
        Assert.Equal("confirmed_email", email.SubKey);
        Assert.Equal("/signin-x", options.CallbackPath);
        Assert.Equal("x-id", options.ClientId);
    }

    [Fact]
    public void AddExternalProviders_AllConfigured_RegistersEverySchemeWithTheExternalSignInScheme()
    {
        using ServiceProvider provider = Build(s_allProviders);

        GoogleOptions google = provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("Google");
        FacebookOptions facebook = provider.GetRequiredService<IOptionsMonitor<FacebookOptions>>().Get("Facebook");
        TwitterAuthenticationOptions x = provider.GetRequiredService<IOptionsMonitor<TwitterAuthenticationOptions>>().Get("X");

        Assert.Equal(AuthenticationSchemes.External, google.SignInScheme);
        Assert.Equal(AuthenticationSchemes.External, facebook.SignInScheme);
        Assert.Equal(AuthenticationSchemes.External, x.SignInScheme);
        Assert.Equal("google-id", google.ClientId);
        Assert.Equal("facebook-id", facebook.AppId);
    }

    [Fact]
    public void AddExternalProviders_AllConfigured_ListsThemInTheCatalog()
    {
        using ServiceProvider provider = Build(s_allProviders);
        var catalog = provider.GetRequiredService<IExternalProviderCatalog>();

        Assert.Equal(["google", "facebook", "x"], catalog.RouteNames);
        Assert.Equal("Google", catalog.FindScheme("google"));
        Assert.Equal("Facebook", catalog.FindScheme("FACEBOOK"));
        Assert.Equal("X", catalog.FindScheme("x"));
        Assert.Equal("x", catalog.FindRouteName("X"));
        Assert.Null(catalog.FindScheme("myspace"));
        Assert.Null(catalog.FindRouteName("Twitter"));
    }

    [Theory]
    [InlineData("Authentication:X:ClientId", "Authentication:X:ClientSecret", "x")]
    [InlineData("Authentication:Google:ClientId", "Authentication:Google:ClientSecret", "google")]
    [InlineData("Authentication:Facebook:AppId", "Authentication:Facebook:AppSecret", "facebook")]
    public async Task AddExternalProviders_OnlyOneSecretConfigured_DoesNotRegisterTheProvider(string idKey, string secretKey, string route)
    {
        Dictionary<string, string?> settings = new()
        {
            [idKey] = "an-id",
            [secretKey] = " ",
        };
        using ServiceProvider provider = Build(settings);

        var catalog = provider.GetRequiredService<IExternalProviderCatalog>();
        IAuthenticationSchemeProvider schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.Empty(catalog.RouteNames);
        Assert.Null(catalog.FindScheme(route));
        Assert.Null(await schemes.GetSchemeAsync("Google"));
        Assert.Null(await schemes.GetSchemeAsync("Facebook"));
        Assert.Null(await schemes.GetSchemeAsync("X"));
    }

    [Fact]
    public void AddExternalProviders_NothingConfigured_RegistersNoProvider()
    {
        using ServiceProvider provider = Build(new Dictionary<string, string?>());

        Assert.Empty(provider.GetRequiredService<IExternalProviderCatalog>().RouteNames);
    }

    [Fact]
    public async Task OnRemoteFailure_ProviderError_RedirectsToTheCallbackInsteadOfThrowing()
    {
        using ServiceProvider provider = Build(s_allProviders);
        GoogleOptions options = provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("Google");
        var http = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("Google", "Google", typeof(GoogleHandler));
        var context = new RemoteFailureContext(http, scheme, options, new InvalidOperationException("access_denied"));

        await options.Events.OnRemoteFailure(context);

        Assert.True(context.Result?.Handled);
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal("/api/v1/auth/external/callback", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task OnTicketReceived_SignedIn_StoresTheSchemeNameInTheProperties()
    {
        using ServiceProvider provider = Build(s_allProviders);
        TwitterAuthenticationOptions options = provider.GetRequiredService<IOptionsMonitor<TwitterAuthenticationOptions>>().Get("X");
        var scheme = new AuthenticationScheme("X", "X", typeof(TwitterAuthenticationHandler));
        var principal = new ClaimsPrincipal(new ClaimsIdentity("Twitter"));
        var context = new TicketReceivedContext(new DefaultHttpContext(), scheme, options, new AuthenticationTicket(principal, "Twitter"))
        {
            Properties = new AuthenticationProperties(),
        };

        await options.Events.OnTicketReceived(context);

        Assert.Equal("X", context.Properties.Items["liftingo.provider"]);
    }

    private static ServiceProvider Build(IDictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddAuthentication().AddCookie(AuthenticationSchemes.External).AddExternalProviders(configuration);

        return services.BuildServiceProvider();
    }
}
