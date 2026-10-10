using Liftingo.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace Liftingo.Api.Extensions;

internal static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>());

        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = RateLimitRejectionHandler.HandleAsync;
        });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((rateLimiterOptions, rateLimitingOptions) =>
            {
                RateLimitingOptions limits = rateLimitingOptions.Value;

                rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    RateLimitPartitionInfo partition = RateLimitPartitionInfo.From(httpContext);

                    TokenBucketPolicyOptions policy = partition.Type == RateLimitPartitionType.User
                        ? limits.Authenticated
                        : limits.Anonymous;

                    return RateLimitPartition.GetTokenBucketLimiter(partition.Key, _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = policy.TokenLimit,
                        TokensPerPeriod = policy.TokensPerPeriod,
                        ReplenishmentPeriod = policy.ReplenishmentPeriod,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
                });
            });

        return services;
    }

    public static WebApplication UseRateLimiting(this WebApplication app)
    {
        app.UseRateLimiter();

        return app;
    }
}