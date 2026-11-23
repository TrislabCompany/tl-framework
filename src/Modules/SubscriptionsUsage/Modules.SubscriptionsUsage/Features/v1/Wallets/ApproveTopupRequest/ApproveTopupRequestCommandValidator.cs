using FluentValidation;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Wallets;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.ApproveTopupRequest;

public sealed class ApproveTopupRequestCommandValidator : AbstractValidator<ApproveTopupRequestCommand>
{
    public ApproveTopupRequestCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
