namespace Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;

/// <summary>A tenant's usage of one meter against its plan. <see cref="Limit"/> <c>null</c> is unlimited.</summary>
public sealed record UsageLimitStatusDto(
    string MeterKey,
    string Name,
    UsageLimitMode Mode,
    long Used,
    long? Limit,
    bool Allowed,
    decimal? ExtraUnitPrice,
    string? Currency);

/// <summary>A meter the product declared, for root's plan editor.</summary>
public sealed record UsageMeterDto(string Key, string Name, UsageLimitMode Mode, long? DefaultLimit, bool IsGauge);

/// <summary>A plan's limit for one meter. <see cref="Limit"/> <c>null</c> is unlimited; <see cref="ExtraUnitPrice"/> is for extra-usage meters.</summary>
public sealed record PlanLimitDto(string MeterKey, long? Limit, decimal? ExtraUnitPrice);
