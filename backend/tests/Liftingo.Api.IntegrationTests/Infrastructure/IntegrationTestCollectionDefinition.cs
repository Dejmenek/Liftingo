namespace Liftingo.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollectionDefinition : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Integration";
}