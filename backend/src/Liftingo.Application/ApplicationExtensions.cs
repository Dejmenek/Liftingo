using Liftingo.Application.Features.Account.Shared;

using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Application;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Helpers shared by the Account slices (concrete classes, not abstractions over EF).
        services.AddSingleton<RefreshTokenCookie>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<SecurityAudit>();
        services.AddScoped<EmailTokenIssuer>();
        services.AddScoped<ConsentRecorder>();
        services.AddScoped<AccountTakeover>();
        services.AddScoped<FrontendRedirects>();

        // In-memory state for e-mails without an account, so it must outlive a request.
        services.AddSingleton<FailedLoginThrottle>();

        return services;
    }
}
