using Trislab.Framework.Shared.Constants;

namespace Trislab.Modules.Auditing.Contracts.Authorization;

public static class AuditingPermissions
{
    public static class AuditTrails
    {
        public const string Resource = nameof(AuditTrails);
        public const string View = $"Permissions.{Resource}.View";
        /// <summary>The caller's own audit records only (<c>GET /audits/mine</c>). Basic, unlike <see cref="View"/>.</summary>
        public const string ViewOwn = $"Permissions.{Resource}.ViewOwn";
        /// <summary>
        /// Allows querying audits across tenants by passing a TenantId filter.
        /// Without this permission, callers can only see their own tenant's audits.
        /// </summary>
        public const string ViewCrossTenant = $"Permissions.{Resource}.ViewCrossTenant";
    }

    public static IReadOnlyList<TrislabPermission> All { get; } =
    [
        new("View Audit Trails", ActionConstants.View, AuditTrails.Resource),
        new("View Own Activity", "ViewOwn", AuditTrails.Resource, IsBasic: true),
        new("View Audit Trails Across Tenants", "ViewCrossTenant", AuditTrails.Resource, IsRoot: true),
    ];
}
