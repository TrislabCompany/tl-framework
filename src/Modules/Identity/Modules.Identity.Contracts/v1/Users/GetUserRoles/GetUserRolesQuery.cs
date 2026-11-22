using Trislab.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Users.GetUserRoles;

public sealed record GetUserRolesQuery(string UserId) : IQuery<List<UserRoleDto>>;