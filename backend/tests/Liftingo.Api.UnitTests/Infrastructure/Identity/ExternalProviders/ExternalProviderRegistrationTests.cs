using Liftingo.Application.Common.Security;
using Liftingo.Infrastructure.Identity.ExternalProviders;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Api.UnitTests.Infrastructure.Identity.ExternalProviders;

public class ExternalProviderRegistrationTests
{
    private static readonly Dictionary<string, string?> s_allConfigured = new()
    {
        ["Authentication:Google:ClientId"] = "1234-abc.apps.googleusercontent.com",
        ["Authentication:Facebook:AppId"] = "987654321",
        ["Authentication:Facebook:AppSecret"] = "facebook-secret",
    };

    [Fact]
    public void AddExternalProviders_AllConfigured_ListsThemWithTheirClientIds()
    {
        using ServiceProvider provider = Build(s_allConfigured);

        var catalog = provider.GetRequiredService<IExternalProviderCatalog>();

        Assert.Equal(
            [
                new ExternalProviderInfo("google", "Google", "1234-abc.apps.googleusercontent.com"),
                new ExternalProviderInfo("facebook", "Facebook", "987654321"),
            ],
            catalog.Providers);
    }

    [Fact]
    public void AddExternalProviders_AllConfigured_RegistersAVerifierPerProvider()
    {
        using ServiceProvider provider = Build(s_allConfigured);

        Assert.Equal(
            ["Google", "Facebook"],
            provider.GetServices<IProviderTokenVerifier>().Select(verifier => verifier.LoginProvider));
    }

    [Theory]
    [InlineData("google")]
    [InlineData("GOOGLE")]
    [InlineData("Google")]
    public void Find_RouteNameInAnyCase_FindsTheProvider(string routeName)
    {
        using ServiceProvider provider = Build(s_allConfigured);

        ExternalProviderInfo? found = provider.GetRequiredService<IExternalProviderCatalog>().Find(routeName);

        Assert.Equal("Google", found?.LoginProvider);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("twitter")]
    [InlineData("")]
    public void Find_UnknownRouteName_ReturnsNull(string routeName)
    {
        using ServiceProvider provider = Build(s_allConfigured);

        Assert.Null(provider.GetRequiredService<IExternalProviderCatalog>().Find(routeName));
    }

    [Fact]
    public void AddExternalProviders_FacebookWithoutSecret_LeavesFacebookOut()
    {
        using ServiceProvider provider = Build(new Dictionary<string, string?>
        {
            ["Authentication:Google:ClientId"] = "1234-abc.apps.googleusercontent.com",
            ["Authentication:Facebook:AppId"] = "987654321",
        });

        var catalog = provider.GetRequiredService<IExternalProviderCatalog>();

        Assert.Equal(["google"], catalog.Providers.Select(p => p.RouteName));
        Assert.Null(catalog.Find("facebook"));
        Assert.Equal(["Google"], provider.GetServices<IProviderTokenVerifier>().Select(verifier => verifier.LoginProvider));
    }

    [Fact]
    public async Task AddExternalProviders_NothingConfigured_ListsNoProviderAndVerifiesNothing()
    {
        using ServiceProvider provider = Build([]);

        Assert.Empty(provider.GetRequiredService<IExternalProviderCatalog>().Providers);

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IExternalIdentityVerifier>();

        Assert.Null(await verifier.VerifyAsync("Google", "any-token", TestContext.Current.CancellationToken));
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExternalProviders(configuration);

        return services.BuildServiceProvider();
    }
}
