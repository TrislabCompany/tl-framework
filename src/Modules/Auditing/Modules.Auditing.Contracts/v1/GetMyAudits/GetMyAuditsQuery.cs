using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Auditing.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Auditing.Contracts.v1.GetMyAudits;

/// <summary>
/// The signed-in user's own activity: the audit records with their user id, in their tenant. The
/// filters of <c>GetAuditsQuery</c> without the tenant and the user, which are always the caller's.
/// </summary>
public sealed class GetMyAuditsQuery : IPagedQuery, IQuery<PagedResponse<AuditSummaryDto>>
{
    public int? PageNumber { get; set; }

    public int? PageSize { get; set; }

    public string? Sort { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public AuditEventType? EventType { get; set; }

    public AuditEventType? ExcludeEventType { get; set; }

    public AuditSeverity? Severity { get; set; }

    public AuditTag? Tags { get; set; }

    public string? Source { get; set; }

    public string? Search { get; set; }
}
