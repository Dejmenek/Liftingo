using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using Assembly = System.Reflection.Assembly;

namespace Liftingo.ArchitectureTests;

public abstract class BaseTest
{
    protected static readonly Assembly DomainAssembly = Assembly.Load("Liftingo.Domain");
    protected static readonly Assembly ApplicationAssembly = Assembly.Load("Liftingo.Application");
    protected static readonly Assembly InfrastructureAssembly = Assembly.Load("Liftingo.Infrastructure");
    protected static readonly Assembly ApiAssembly = Assembly.Load("Liftingo.Api");

    protected static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            DomainAssembly,
            ApplicationAssembly,
            InfrastructureAssembly,
            ApiAssembly)
        .Build();
}