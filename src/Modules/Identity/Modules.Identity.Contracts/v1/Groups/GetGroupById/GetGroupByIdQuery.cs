using Trislab.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Groups.GetGroupById;

public sealed record GetGroupByIdQuery(Guid Id) : IQuery<GroupDto>;