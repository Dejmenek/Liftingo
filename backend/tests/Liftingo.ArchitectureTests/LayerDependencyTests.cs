using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Liftingo.ArchitectureTests;

public class LayerDependencyTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            System.Reflection.Assembly.Load("Liftingo.Domain"),
            System.Reflection.Assembly.Load("Liftingo.Application"),
            System.Reflection.Assembly.Load("Liftingo.Infrastructure"),
            System.Reflection.Assembly.Load("Liftingo.Api"))
        .Build();

    private static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly("Liftingo.Domain").As("Domain");

    private static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly("Liftingo.Application").As("Application");

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly("Liftingo.Infrastructure").As("Infrastructure");

    private static readonly IObjectProvider<IType> ApiLayer =
        Types().That().ResideInAssembly("Liftingo.Api").As("Api");

    private static readonly IObjectProvider<IType> ApplicationInfrastructureOrApiLayer =
        Types().That().ResideInAssembly("Liftingo.Application")
            .Or().ResideInAssembly("Liftingo.Infrastructure")
            .Or().ResideInAssembly("Liftingo.Api")
            .As("Application, Infrastructure or Api");

    private static readonly IObjectProvider<IType> InfrastructureOrApiLayer =
        Types().That().ResideInAssembly("Liftingo.Infrastructure")
            .Or().ResideInAssembly("Liftingo.Api")
            .As("Infrastructure or Api");

    [Fact]
    public void Domain_ShouldNotDependOn_ApplicationInfrastructureOrApi()
    {
        Types().That().Are(DomainLayer).Should()
            .NotDependOnAny(ApplicationInfrastructureOrApiLayer)
            .Because("Domain must not depend on anything else")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Application_ShouldNotDependOn_InfrastructureOrApi()
    {
        Types().That().Are(ApplicationLayer).Should()
            .NotDependOnAny(InfrastructureOrApiLayer)
            .Because("Application must only depend on Domain")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Api()
    {
        Types().That().Are(InfrastructureLayer).Should()
            .NotDependOnAny(ApiLayer)
            .Because("Api depends on Infrastructure, not the other way around")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }
}