using Trislab.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Multitenancy.Contracts.v1.TenantProvisioning;

public sealed record GetTenantProvisioningStatusQuery(string TenantId) : IQuery<TenantProvisioningStatusDto>;