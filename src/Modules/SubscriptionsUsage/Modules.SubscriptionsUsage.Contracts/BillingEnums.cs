namespace Trislab.Modules.SubscriptionsUsage.Contracts;

public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    Void = 3
}

public enum SubscriptionStatus
{
    Active = 0,
    Suspended = 1,
    Cancelled = 2
}

public enum InvoiceLineItemKind
{
    BaseFee = 0,
    Overage = 1,
    Adjustment = 2
}

public enum PlanInterval
{
    Monthly = 0,
    Yearly = 1
}

public enum InvoicePurpose
{
    // Usage=0 doubles as the column default (rows backfill to Usage; Subscription=1 always written
    // explicitly). Do NOT reorder — making Subscription 0 reintroduces the EF default-omitted bug.
    Usage = 0,
    Subscription = 1,
    Topup = 2
}

public enum WalletStatus
{
    Active = 0,
    Frozen = 1
}

public enum WalletTransactionKind
{
    Topup = 0,
    MessageCharge = 1,
    Adjustment = 2
}

public enum TopupRequestStatus
{
    Pending = 0,
    Invoiced = 1,
    Completed = 2,
    Rejected = 3,
    Cancelled = 4
}

/// <summary>What happens when a tenant reaches a meter's limit.</summary>
public enum UsageLimitMode
{
    /// <summary>The action is refused at the limit (members, items, storage).</summary>
    HardLimit = 0,

    /// <summary>The action is allowed beyond the limit; the units above it are extra usage at the plan's unit price.</summary>
    ExtraUsage = 1
}
