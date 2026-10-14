using ArchUnitNET.Domain;
using ArchUnitNET.xUnitV3;
using Liftingo.Application.Common.Messaging;
using System.Text.RegularExpressions;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Liftingo.ArchitectureTests;

/// <summary>
/// What each layer may contain, beyond which layer may reference which.
/// </summary>
public partial class LayerResponsibilityTests : BaseTest
{
    // What the compiled Application code may reference. Anything else needs a constitution amendment.
    private static readonly string[] s_applicationAllowedAssemblies =
    [
        "Liftingo.Domain",
        "netstandard",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Primitives",
        "FluentValidation",
    ];

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly(InfrastructureAssembly).As("Infrastructure");

    [Fact]
    public void ApplicationAssembly_ReferencesOnlyTheAllowlistedAssemblies()
    {
        string[] notAllowed =
        [
            .. ApplicationAssembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => !name.StartsWith("System", StringComparison.Ordinal))
                .Where(name => !s_applicationAllowedAssemblies.Contains(name, StringComparer.Ordinal)),
        ];

        Assert.Empty(notAllowed);
    }

    [Theory]
    [InlineData("^Microsoft\\.AspNetCore(\\..*)?$")]
    [InlineData("^Microsoft\\.Extensions\\.Caching(\\..*)?$")]
    [InlineData("^StackExchange\\.Redis(\\..*)?$")]
    [InlineData("^Microsoft\\.EntityFrameworkCore\\.SqlServer(\\..*)?$")]
    public void ApplicationLayer_ShouldNotDependOn_HttpCachingRedisOrTheSqlServerProvider(string namespacePattern)
    {
        Types().That().ResideInAssembly(ApplicationAssembly).Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(namespacePattern)
            .Because("Application holds use cases, not HTTP, caching or storage details")
            .Check(Architecture);
    }

    [Fact]
    public void ApplicationLayer_HasNoEndpoints()
    {
        Types().That().ResideInAssembly(ApplicationAssembly).Should()
            .NotHaveNameEndingWith("Endpoint")
            .Because("Api maps HTTP to use cases")
            .Check(Architecture);
    }

    [Fact]
    public void ApplicationHandlers_AreCommandOrQueryHandlersInAUseCaseFolder()
    {
        string[] misplaced =
        [
            .. Handlers()
                .Where(handler => !UseCaseNamespace().IsMatch(handler.Namespace ?? string.Empty))
                .Select(handler => handler.FullName!),
        ];

        Assert.Empty(misplaced);
    }

    [Fact]
    public void ApplicationHandlers_ImplementTheInterfaceTheirNameSays()
    {
        string[] mismatched =
        [
            .. ApplicationAssembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } && !type.IsNested)
                .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal) || IsHandler(type))
                .Where(type => !(type.Name.EndsWith("CommandHandler", StringComparison.Ordinal)
                        && Implements(type, typeof(ICommandHandler<>), typeof(ICommandHandler<,>))
                    || type.Name.EndsWith("QueryHandler", StringComparison.Ordinal)
                        && Implements(type, typeof(IQueryHandler<,>))))
                .Select(type => type.FullName!),
        ];

        Assert.Empty(mismatched);
    }

    [Fact]
    public void ApplicationCommandsAndQueries_ImplementTheInterfaceTheirNameSays()
    {
        string[] mismatched =
        [
            .. ApplicationAssembly.GetTypes()
                .Where(type => UseCaseNamespace().IsMatch(type.Namespace ?? string.Empty))
                .Where(type => type.Name.EndsWith("Command", StringComparison.Ordinal)
                    || type.Name.EndsWith("Query", StringComparison.Ordinal)
                    || Implements(type, typeof(ICommand), typeof(ICommand<>), typeof(IQuery<>)))
                .Where(type => !(type.Name.EndsWith("Command", StringComparison.Ordinal)
                        && Implements(type, typeof(ICommand), typeof(ICommand<>))
                    || type.Name.EndsWith("Query", StringComparison.Ordinal)
                        && Implements(type, typeof(IQuery<>))))
                .Select(type => type.FullName!),
        ];

        Assert.Empty(mismatched);
    }

    [Fact]
    public void ApplicationHandlers_DoNotDependOnOtherHandlers()
    {
        string[] calls =
        [
            .. Handlers()
                .SelectMany(handler => handler.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Where(parameter => IsHandler(parameter.ParameterType)
                        || parameter.ParameterType.Name.EndsWith("Handler", StringComparison.Ordinal))
                    .Select(parameter => $"{handler.Name} -> {parameter.ParameterType.Name}")),
        ];

        Assert.Empty(calls);
    }

    [Fact]
    public void ApiLayer_UsesInfrastructureOnlyInItsWiring()
    {
        Types().That().ResideInAssembly(ApiAssembly)
            .And().DoNotResideInNamespace("Liftingo.Api.Extensions")
            .And().DoNotHaveFullNameMatching("^Program($|\\+)")
            .Should().NotDependOnAny(InfrastructureLayer)
            .Because("Api references Infrastructure only to wire up DI at startup")
            .Check(Architecture);
    }

    [Fact]
    public void ApiLayer_ShouldNotDependOn_TheDbContext()
    {
        Types().That().ResideInAssembly(ApiAssembly).Should()
            .NotDependOnAnyTypesThat()
            .HaveFullName("Liftingo.Application.Common.Persistence.IApplicationDbContext")
            .Because("Api has no database access")
            .Check(Architecture);
    }

    private static IEnumerable<Type> Handlers() =>
        ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && !type.IsNested)
            .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal) || IsHandler(type));

    private static bool IsHandler(Type type) =>
        Implements(type, typeof(ICommandHandler<>), typeof(ICommandHandler<,>), typeof(IQueryHandler<,>));

    private static bool Implements(Type type, params Type[] interfaces) =>
        (type.IsInterface ? [type, .. type.GetInterfaces()] : type.GetInterfaces())
            .Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i)
            .Any(interfaces.Contains);

    [GeneratedRegex("^Liftingo\\.Application\\.Features\\.[A-Za-z]+\\.[A-Za-z]+$")]
    private static partial Regex UseCaseNamespace();
}