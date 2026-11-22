using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Contracts.v1.Roles.GetRoles;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Roles.GetRoles;

public sealed class GetRolesQueryHandler : IQueryHandler<GetRolesQuery, PagedResponse<RoleDto>>
{
    private readonly IRoleService _roleService;

    public GetRolesQueryHandler(IRoleService roleService)
    {
        _roleService = roleService;
    }

    public async ValueTask<PagedResponse<RoleDto>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _roleService.GetRolesAsync(
            query.PageNumber ?? 1,
            query.PageSize ?? 20,
            query.Search,
            cancellationToken).ConfigureAwait(false);
    }
}
