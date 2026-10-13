using System.Reflection;
using System.Runtime.CompilerServices;

using FluentValidation;

using Liftingo.Application.Features.Account.Shared;

using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Application;

public static class ApplicationExtensions
{
    private const string CommandHandlerSuffix = "CommandHandler";
    private const string QueryHandlerSuffix = "QueryHandler";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Handlers and validators are found by convention (R22): every *CommandHandler and *QueryHandler, and every
        // FluentValidation validator, as the concrete type the handler or endpoint injects.
        foreach (Type type in typeof(ApplicationExtensions).Assembly.GetTypes().Where(IsHandlerOrValidator))
        {
            services.AddScoped(type);
        }

        // Helpers shared by the Account slices (concrete classes, not abstractions over EF).
        services.AddScoped<SessionIssuer>();
        services.AddScoped<SecurityAudit>();
        services.AddScoped<EmailTokenIssuer>();
        services.AddScoped<ConsentRecorder>();
        services.AddScoped<AccountTakeover>();

        return services;
    }

    private static bool IsHandlerOrValidator(Type type) =>
        IsConcreteClass(type)
        && (type.Name.EndsWith(CommandHandlerSuffix, StringComparison.Ordinal)
            || type.Name.EndsWith(QueryHandlerSuffix, StringComparison.Ordinal)
            || IsValidator(type));

    private static bool IsConcreteClass(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetCustomAttribute<CompilerGeneratedAttribute>() is null;

    private static bool IsValidator(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>));
}
