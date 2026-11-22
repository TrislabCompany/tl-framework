using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Roles.DeleteRole;

public sealed record DeleteRoleCommand(string Id) : ICommand<Unit>;