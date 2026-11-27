using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Impersonation.GetImpersonationRequests;

/// <summary>The requests the signed-in operator made, newest first; Status null = all.</summary>
public sealed record GetSentImpersonationRequestsQuery(
    ImpersonationRequestStatus? Status = null,
    int Take = 100)
    : IQuery<IReadOnlyList<ImpersonationRequestDto>>;
