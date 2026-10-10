using ArchUnitNET.Domain;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Liftingo.ArchitectureTests;

public class LayerDependencyTests : BaseTest
{
    private static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly(DomainAssembly).As("Domain");

    private static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly(ApplicationAssembly).As("Application");

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly(InfrastructureAssembly).As("Infrastructure");

    private static readonly IObjectProvider<IType> ApiLayer =
        Types().That().ResideInAssembly(ApiAssembly).As("Api");

    private static readonly IObjectProvider<IType> ApplicationInfrastructureOrApiLayer =
        Types().That().ResideInAssembly(ApplicationAssembly, InfrastructureAssembly, ApiAssembly)
            .As("Application, Infrastructure or Api");

    private static readonly IObjectProvider<IType> InfrastructureOrApiLayer =
        Types().That().ResideInAssembly(InfrastructureAssembly, ApiAssembly)
            .As("Infrastructure or Api");

    [Fact]
    public void Domain_ShouldNotDependOn_ApplicationInfrastructureOrApi()
    {
        Types().That().Are(DomainLayer).Should()
            .NotDependOnAny(ApplicationInfrastructureOrApiLayer)
            .Because("Domain must not depend on anything else")
            .Check(Architecture);
    }

    [Fact]
    public void Application_ShouldNotDependOn_InfrastructureOrApi()
    {
        Types().That().Are(ApplicationLayer).Should()
            .NotDependOnAny(InfrastructureOrApiLayer)
            .Because("Application must only depend on Domain")
            .Check(Architecture);
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Api()
    {
        Types().That().Are(InfrastructureLayer).Should()
            .NotDependOnAny(ApiLayer)
            .Because("Api depends on Infrastructure, not the other way around")
            .Check(Architecture);
    }
}