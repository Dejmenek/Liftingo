using System.Reflection;
using System.Runtime.CompilerServices;

using FluentValidation;

using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.Extensions.DependencyInjection;

namespace Liftingo.Application;

public static class ApplicationExtensions
{
    private static readonly Type[] s_handlerInterfaces =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (Type type in typeof(ApplicationExtensions).Assembly.GetTypes().Where(IsConcreteClass))
        {
        // Helpers shared by the Account slices (concrete classes, not abstractions over EF).
            foreach (Type handlerInterface in type.GetInterfaces().Where(IsHandlerInterface))
            {
                services.AddScoped(handlerInterface, type);
            }

            if (IsValidator(type))
            {
                services.AddScoped(type);
            }
        }

        services.AddScoped<SessionIssuer>();
        services.AddScoped<SecurityAudit>();
        services.AddScoped<EmailTokenIssuer>();
        services.AddScoped<ConsentRecorder>();
        services.AddScoped<AccountTakeover>();

        return services;
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && s_handlerInterfaces.Contains(type.GetGenericTypeDefinition());

    private static bool IsConcreteClass(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetCustomAttribute<CompilerGeneratedAttribute>() is null;

    private static bool IsValidator(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>));
}
