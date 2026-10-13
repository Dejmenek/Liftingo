using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Liftingo.ArchitectureTests;

/// <summary>
/// What each layer may contain, beyond which layer may reference which (R25, constitution I and II).
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
                .Where(handler => !handler.Name.EndsWith("CommandHandler", StringComparison.Ordinal)
                    && !handler.Name.EndsWith("QueryHandler", StringComparison.Ordinal)
                    || !UseCaseNamespace().IsMatch(handler.Namespace ?? string.Empty))
                .Select(handler => handler.FullName!),
        ];

        Assert.Empty(misplaced);
    }

    [Fact]
    public void ApplicationHandlers_DoNotDependOnOtherHandlers()
    {
        string[] calls =
        [
            .. Handlers()
                .SelectMany(handler => handler.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Where(parameter => parameter.ParameterType.Name.EndsWith("Handler", StringComparison.Ordinal))
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
            .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal));

    [GeneratedRegex("^Liftingo\\.Application\\.Features\\.[A-Za-z]+\\.[A-Za-z]+$")]
    private static partial Regex UseCaseNamespace();
}
