using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace Liftingo.Api.Extensions;

internal static class ForwardedHeadersExtensions
{
    public const string KnownProxiesSection = "ForwardedHeaders:KnownProxies";

    // Behind App Service the socket address is the front end's. The audit log needs the client's.
    public static IServiceCollection AddForwardedHeaderSupport(this IServiceCollection services, IConfiguration configuration)
    {
        string[] knownProxies = configuration.GetSection(KnownProxiesSection).Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (string proxy in knownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        return services;
    }

    public static WebApplication UseForwardedHeaderSupport(this WebApplication app)
    {
        app.UseForwardedHeaders();

        return app;
    }
}