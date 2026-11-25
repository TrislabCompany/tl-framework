namespace Integration.Tests.Infrastructure;

/// <summary>The multi-tenant host. Not parallel with the single-tenant collection: see <see cref="SingleTenantCollectionDefinition"/>.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TrislabCollectionDefinition : ICollectionFixture<TrislabWebApplicationFactory>
{
    public const string Name = "TrislabIntegration";
}

/// <summary>
/// The single-tenant host. Each module that supports single-tenant mode has tests in this collection (a test class
/// written once as an abstract base, with a derived class per mode). The two hosts share process-wide statics
/// (the module loader, the audit publisher), so the two collections run one after the other.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SingleTenantCollectionDefinition : ICollectionFixture<SingleTenantWebApplicationFactory>
{
    public const string Name = "TrislabIntegrationSingleTenant";
}
