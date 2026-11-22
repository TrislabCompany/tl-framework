using Trislab.Modules.Multitenancy.Contracts;
using Trislab.Modules.Multitenancy.Contracts.Dtos;
using Trislab.Modules.Multitenancy.Contracts.v1.GetTenantStatus;
using Mediator;

namespace Trislab.Modules.Multitenancy.Features.v1.GetTenantStatus;

public sealed class GetTenantStatusQueryHandler(ITenantService tenantService)
    : IQueryHandler<GetTenantStatusQuery, TenantStatusDto>
{
    public async ValueTask<TenantStatusDto> Handle(GetTenantStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await tenantService.GetStatusAsync(query.TenantId, cancellationToken).ConfigureAwait(false);
    }
}