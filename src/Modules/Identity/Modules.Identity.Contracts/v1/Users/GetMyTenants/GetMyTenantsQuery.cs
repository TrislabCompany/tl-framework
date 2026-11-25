using System.Collections.ObjectModel;
using Trislab.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Users.GetMyTenants;

/// <summary>The tenants the signed-in user is an active member of, for the tenant menu.</summary>
public sealed record GetMyTenantsQuery : IQuery<ReadOnlyCollection<TenantMembershipDto>>;
