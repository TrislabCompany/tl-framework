using Trislab.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Multitenancy.Contracts.v1.ChangeTenantActivation;

public sealed record ChangeTenantActivationCommand(string TenantId, bool IsActive)
    : ICommand<TenantLifecycleResultDto>;