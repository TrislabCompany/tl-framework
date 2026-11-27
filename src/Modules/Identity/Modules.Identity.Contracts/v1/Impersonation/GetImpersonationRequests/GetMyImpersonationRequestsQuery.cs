using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Impersonation.GetImpersonationRequests;

/// <summary>
/// The requests to impersonate the signed-in user in the current tenant that still need an answer or can still be
/// taken back: pending, approved and used ones whose impersonation may be running. Newest first.
/// </summary>
public sealed record GetMyImpersonationRequestsQuery : IQuery<IReadOnlyList<ImpersonationRequestDto>>;
