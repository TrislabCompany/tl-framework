namespace Trislab.Modules.SubscriptionsUsage.Domain;

/// <summary>
/// A plan's limit for one product-declared meter. <see cref="Limit"/> <c>null</c> is unlimited.
/// <see cref="ExtraUnitPrice"/> is the price of one unit above the limit, for extra-usage meters.
/// </summary>
public sealed record PlanLimit(string MeterKey, long? Limit, decimal? ExtraUnitPrice);
