using Liftingo.Api.RequestTimeouts;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Liftingo.Api.Extensions;

internal static class RequestTimeoutsExtensions
{
    public static IServiceCollection AddRequestTimeoutPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RequestTimeoutsOptions>, RequestTimeoutsOptionsValidator>());

        services.AddOptions<RequestTimeoutsOptions>()
            .Bind(configuration.GetSection(RequestTimeoutsOptions.SectionName))
            .ValidateOnStart();

        services.AddRequestTimeouts();

        services.AddOptions<RequestTimeoutOptions>()
            .Configure<IOptions<RequestTimeoutsOptions>>((timeoutOptions, requestTimeoutsOptions) =>
            {
                RequestTimeoutsOptions timeouts = requestTimeoutsOptions.Value;

                timeoutOptions.DefaultPolicy = CreatePolicy(RequestTimeoutResponseHandler.DefaultPolicyName, timeouts.DefaultTimeout);

                foreach ((string name, TimeSpan timeout) in timeouts.Policies)
                {
                    timeoutOptions.AddPolicy(name, CreatePolicy(name, timeout));
                }
            });

        return services;
    }

    public static WebApplication UseRequestTimeoutPolicies(this WebApplication app)
    {
        app.UseRequestTimeouts();

        return app;
    }

    private static RequestTimeoutPolicy CreatePolicy(string name, TimeSpan timeout) =>
        new()
        {
            Timeout = timeout,
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = RequestTimeoutResponseHandler.Create(name),
        };
}