using FluentValidation;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Wallets;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.GetTopupRequests;

public sealed class GetTopupRequestsQueryValidator : AbstractValidator<GetTopupRequestsQuery>
{
    public GetTopupRequestsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
