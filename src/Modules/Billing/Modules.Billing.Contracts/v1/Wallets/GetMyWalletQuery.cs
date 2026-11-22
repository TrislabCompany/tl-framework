using Trislab.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Billing.Contracts.v1.Wallets;

public sealed record GetMyWalletQuery : IQuery<WalletDto>;
