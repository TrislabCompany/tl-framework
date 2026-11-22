using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Billing.Contracts.v1.Wallets;

public sealed record GetMyTopupRequestsQuery(
    TopupRequestStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<TopupRequestDto>>;
