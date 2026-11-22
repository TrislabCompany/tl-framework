using Trislab.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Multitenancy.Contracts.v1.GetTenantMigrations;

public sealed record GetTenantMigrationsQuery : IQuery<IReadOnlyCollection<TenantMigrationStatusDto>>;