using Liftingo.Application.Common.Configuration;
using Liftingo.Application.Common.Email;
using Liftingo.Application.Common.Persistence;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;
using Liftingo.Infrastructure.Audit;
using Liftingo.Infrastructure.Email;
using Liftingo.Infrastructure.Identity;
using Liftingo.Infrastructure.Persistence;
using Liftingo.Infrastructure.Sessions;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using StackExchange.Redis;

namespace Liftingo.Infrastructure;

public static class InfrastructureExtensions
{
    // A dead Redis has to fail a request quickly with a 503 (FR-015c), not hold it for the library default.
    private const int RedisConnectTimeoutMilliseconds = 2000;
    private const int RedisCommandTimeoutMilliseconds = 2000;

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        services.TryAddSingleton(TimeProvider.System);

        services.AddPersistence(configuration);
        services.AddAccountOptions(configuration);
        services.AddRedisSessions();
        services.AddIdentityServices(configuration);
        services.AddEmail(configuration, environment);
        services.AddSecurityAudit(environment);

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

    private static void AddAccountOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<SessionsOptions, SessionsOptionsValidator>(configuration, SessionsOptions.SectionName);
        services.AddValidatedOptions<KnownDevicesOptions, KnownDevicesOptionsValidator>(configuration, KnownDevicesOptions.SectionName);
        services.AddValidatedOptions<EmailTokensOptions, EmailTokensOptionsValidator>(configuration, EmailTokensOptions.SectionName);
        services.AddValidatedOptions<ExternalLoginOptions, ExternalLoginOptionsValidator>(configuration, ExternalLoginOptions.SectionName);
        services.AddValidatedOptions<AuditOptions, AuditOptionsValidator>(configuration, AuditOptions.SectionName);
        services.AddValidatedOptions<LegalOptions, LegalOptionsValidator>(configuration, LegalOptions.SectionName);
        services.AddValidatedOptions<AppOptions, AppOptionsValidator>(configuration, AppOptions.SectionName);
        services.AddValidatedOptions<JwtOptions, JwtOptionsValidator>(configuration, JwtOptions.SectionName);
    }

    private static void AddValidatedOptions<TOptions, TValidator>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TOptions>, TValidator>());

        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();
    }

    private static void AddRedisSessions(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var redisOptions = ConfigurationOptions.Parse(
                provider.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value.Redis);

            // The default (true) would crash startup while Redis is down.
            redisOptions.AbortOnConnectFail = false;

            // StackExchange.Redis 3.x queues commands while disconnected until their timeout. FailFast
            // fails them at once, so an outage answers 503 quickly.
            redisOptions.BacklogPolicy = BacklogPolicy.FailFast;
            redisOptions.ConnectTimeout = RedisConnectTimeoutMilliseconds;
            redisOptions.AsyncTimeout = RedisCommandTimeoutMilliseconds;
            redisOptions.SyncTimeout = RedisCommandTimeoutMilliseconds;

            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddSingleton<ISessionStore, RedisSessionStore>();
        services.AddSingleton<ILoginThrottle, RedisLoginThrottle>();
    }

    private static void AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<IdentitySecurityOptions>, IdentitySecurityOptionsValidator>());

        services.AddOptions<IdentitySecurityOptions>()
            .Bind(configuration.GetSection(IdentitySecurityOptions.SectionName))
            .ValidateOnStart();

        // Data Protection wraps the audit keys and encrypts the external-login cookie, so its key ring
        // has to survive restarts.
        services.AddDataProtection()
            .SetApplicationName("Liftingo")
            .PersistKeysToDbContext<ApplicationDbContext>();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

                options.User.RequireUniqueEmail = true;

                options.Lockout.AllowedForNewUsers = true;
            })
            .AddUserStore<LiftingoUserStore>()
            .AddDefaultTokenProviders();

        // Replace (not add) the default validator, or every password error would be reported twice.
        services.Replace(ServiceDescriptor.Scoped<IPasswordValidator<ApplicationUser>, LiftingoPasswordValidator>());

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

        // The same lockout values for e-mails without an account (RedisLoginThrottle), so the
        // answer for an unknown address matches a real one.
        services.AddSingleton<IOptions<LoginLockoutOptions>>(provider =>
        {
            LockoutPolicyOptions lockout = provider.GetRequiredService<IOptions<IdentitySecurityOptions>>().Value.Lockout;

            return Options.Create(new LoginLockoutOptions
            {
                MaxFailedAccessAttempts = lockout.MaxFailedAccessAttempts,
                Duration = lockout.Duration,
            });
        });

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // Api registers the JWT bearer scheme; this configures it (R20).
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<JwtBearerOptions>, JwtBearerOptionsSetup>());
    }

    // E-mails are rendered here and handed to a transport. The console transport prints them (links included)
    // for the developer; everywhere else mail is dropped until the Azure Communication Services transport exists.
    private static void AddEmail(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddValidatedOptions<EmailOptions, EmailOptionsValidator>(configuration, EmailOptions.SectionName);

        services.AddSingleton<AccountEmailRenderer>();
        services.AddSingleton<IEmailSender, EmailSender>();

        if (environment.IsDevelopment())
        {
            services.AddSingleton<IEmailTransport, ConsoleEmailTransport>();
        }
        else
        {
            services.AddSingleton<IEmailTransport, DisabledEmailTransport>();
        }
    }

    private static void AddSecurityAudit(this IServiceCollection services, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            services.AddSingleton<IAuditSink, ConsoleAuditSink>();
        }
        else
        {
            services.AddSingleton<IAuditSink, DisabledAuditSink>();
        }

        services.AddScoped<IAuditKeyProvider, AuditKeyProvider>();
        services.AddScoped<ISecurityAuditLog, EncryptingSecurityAuditLog>();
    }
}
