using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Data;

namespace Trislab.Modules.Identity.Services;

/// <summary>
/// Writes Identity's security events to the audit trail with one shape: the action, the subject (the account
/// the event is about), who did it and a few ids. Never a password, token or e-mail body.
/// </summary>
/// <remarks>
/// The audit record takes its tenant from the ambient tenant, so write it inside the tenant the event is about.
/// An event about the account itself (a password reset, a lockout) goes through <see cref="WriteForAccountAsync"/>.
/// </remarks>
public sealed partial class IdentityAuditor(
    IAuditClient auditClient,
    IdentityDbContext db,
    IEventTenantScope tenantScope,
    ILogger<IdentityAuditor> logger)
{
    public ValueTask WriteAsync(
        SecurityAction action,
        string? subjectId,
        string reasonCode,
        IReadOnlyDictionary<string, object?>? data = null,
        AuditSeverity severity = AuditSeverity.Information,
        CancellationToken ct = default) =>
        auditClient.WriteSecurityAsync(
            action,
            subjectId: subjectId,
            reasonCode: reasonCode,
            claims: data,
            severity: severity,
            source: "Identity",
            ct: ct);

    /// <summary>
    /// Writes an event about the account itself (its password, a lockout) to every tenant the account is an accepted
    /// member of, so each tenant's audit trail shows what happened to its member. Never to the request's tenant as
    /// such: an anonymous caller may name any tenant, and must not plant records about an account in a tenant it
    /// doesn't belong to. An account with no membership gets one record in the root tenant.
    /// The work is done when this runs, so a failed write is logged and never changes the caller's answer.
    /// </summary>
    public async Task WriteForAccountAsync(
        SecurityAction action,
        string userId,
        string reasonCode,
        IReadOnlyDictionary<string, object?>? data = null,
        AuditSeverity severity = AuditSeverity.Information,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        List<string> tenantIds;
        try
        {
            tenantIds = await db.TenantMemberships
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.AcceptedOnUtc != null)
                .Select(m => m.TenantId)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAccountAuditFailed(logger, ex, action, null);
            return;
        }

        if (tenantIds.Count == 0)
        {
            tenantIds.Add(MultitenancyConstants.Root.Id);
        }

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await tenantScope.ExecuteAsync(
                    tenantId,
                    async token => await WriteAsync(action, userId, reasonCode, data, severity, token).ConfigureAwait(false),
                    ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogAccountAuditFailed(logger, ex, action, tenantId);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Writing the account audit event {Action} failed in tenant {TenantId}")]
    private static partial void LogAccountAuditFailed(ILogger logger, Exception exception, SecurityAction action, string? tenantId);
}
