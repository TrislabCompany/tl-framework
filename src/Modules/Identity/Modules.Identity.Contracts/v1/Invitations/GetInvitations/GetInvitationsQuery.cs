using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Invitations.GetInvitations;

/// <summary>A page of the current tenant's invitations, newest first.</summary>
public sealed class GetInvitationsQuery : IPagedQuery, IQuery<PagedResponse<InvitationDto>>
{
    public int? PageNumber { get; set; }

    public int? PageSize { get; set; }

    /// <summary>Not used: invitations are always newest first.</summary>
    public string? Sort { get; set; }
}
