using FluentValidation;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Wallets;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.RejectTopupRequest;

public sealed class RejectTopupRequestCommandValidator : AbstractValidator<RejectTopupRequestCommand>
{
    public RejectTopupRequestCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}
