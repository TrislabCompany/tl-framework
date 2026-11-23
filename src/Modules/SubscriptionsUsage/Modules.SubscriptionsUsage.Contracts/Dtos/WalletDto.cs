namespace Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;

public sealed record WalletDto(
    Guid Id,
    string TenantId,
    string Currency,
    decimal Balance,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<WalletTransactionDto> RecentTransactions);
