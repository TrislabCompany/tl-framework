using Trislab.Framework.Shared.Constants;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.Authorization;

/// <summary>
/// <see cref="View"/> is a tenant's own billing: its subscription, invoices, usage and wallet.
/// <see cref="Manage"/> is the operator's work across tenants (plans, subscriptions, invoices,
/// top-up approval, usage capture), so it is root-only: a tenant admin who held it could change the
/// global plans, or approve its own top-up and mark the invoice paid.
/// </summary>
public static class BillingPermissions
{
    public const string Resource = "Billing";
    public const string View   = $"Permissions.{Resource}.View";
    public const string Manage = $"Permissions.{Resource}.Manage";

    public static IReadOnlyList<TrislabPermission> All { get; } =
    [
        new("View Billing",   ActionConstants.View, Resource, IsBasic: true),
        new("Manage Billing", "Manage",             Resource, IsRoot: true),
    ];
}
