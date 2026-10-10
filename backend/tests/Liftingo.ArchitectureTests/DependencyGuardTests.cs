using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Liftingo.ArchitectureTests;

public class DependencyGuardTests : BaseTest
{
    [Fact]
    public void DomainLayer_ShouldNotDependOn_AspNetCoreIdentity()
    {
        Types().That().ResideInAssembly(DomainAssembly).Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore.Identity")
            .Check(Architecture);
    }

    [Fact]
    public void ApplicationLayer_ShouldNotDependOn_AspNetCoreIdentity()
    {
        Types().That().ResideInAssembly(ApplicationAssembly).Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespace("Microsoft.AspNetCore.Identity")
            .Check(Architecture);
    }
}