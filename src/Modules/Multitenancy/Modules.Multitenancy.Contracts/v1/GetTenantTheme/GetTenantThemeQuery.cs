using Trislab.Modules.Multitenancy.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Multitenancy.Contracts.v1.GetTenantTheme;

public sealed record GetTenantThemeQuery : IQuery<TenantThemeDto>;