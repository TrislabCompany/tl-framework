using FluentValidation;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Wallets;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.CreateTopupRequest;

public sealed class CreateTopupRequestCommandValidator : AbstractValidator<CreateTopupRequestCommand>
{
    public CreateTopupRequestCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m).LessThanOrEqualTo(1_000_000m);
        RuleFor(x => x.Note).MaximumLength(512);
    }
}
