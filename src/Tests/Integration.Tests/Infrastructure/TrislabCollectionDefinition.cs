namespace Integration.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class TrislabCollectionDefinition : ICollectionFixture<TrislabWebApplicationFactory>
{
    public const string Name = "TrislabIntegration";
}
