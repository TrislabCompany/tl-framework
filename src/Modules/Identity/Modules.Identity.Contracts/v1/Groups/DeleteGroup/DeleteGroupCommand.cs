using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Groups.DeleteGroup;

public sealed record DeleteGroupCommand(Guid Id) : ICommand<Unit>;