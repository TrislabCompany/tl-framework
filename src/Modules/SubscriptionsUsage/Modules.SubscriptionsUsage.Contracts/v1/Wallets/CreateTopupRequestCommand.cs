using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Wallets;

public sealed record CreateTopupRequestCommand(decimal Amount, string? Note) : ICommand<Guid>;
