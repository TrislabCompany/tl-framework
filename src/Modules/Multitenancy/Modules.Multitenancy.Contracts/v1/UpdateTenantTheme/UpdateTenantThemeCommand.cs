using Trislab.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Multitenancy.Contracts.v1.UpdateTenantTheme;

public sealed record UpdateTenantThemeCommand(TenantThemeDto Theme) : ICommand;