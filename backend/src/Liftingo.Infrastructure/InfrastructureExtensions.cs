using Liftingo.Application.Common;
using Liftingo.Application.Common.Persistence;
using Liftingo.Infrastructure.Identity;
using Liftingo.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Liftingo.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddIdentityServices(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();

        return services;
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ConnectionStringsOptions>, ConnectionStringsOptionsValidator>());

        services.AddOptions<ConnectionStringsOptions>()
            .Bind(configuration.GetSection(ConnectionStringsOptions.SectionName))
            .ValidateOnStart();

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseSqlServer(provider.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value.Default));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
    }

    private static void AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<IdentitySecurityOptions>, IdentitySecurityOptionsValidator>());

        services.AddOptions<IdentitySecurityOptions>()
            .Bind(configuration.GetSection(IdentitySecurityOptions.SectionName))
            .ValidateOnStart();

        services.AddDataProtection();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

                options.User.RequireUniqueEmail = true;

                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<IdentitySecurityOptions>>((identityOptions, securityOptions) =>
            {
                IdentitySecurityOptions security = securityOptions.Value;

                identityOptions.Password.RequiredLength = security.Password.RequiredLength;
                identityOptions.Password.RequireDigit = security.Password.RequireDigit;
                identityOptions.Password.RequireNonAlphanumeric = security.Password.RequireSpecialCharacter;

                identityOptions.Lockout.MaxFailedAccessAttempts = security.Lockout.MaxFailedAccessAttempts;
                identityOptions.Lockout.DefaultLockoutTimeSpan = security.Lockout.Duration;
            });
    }
}