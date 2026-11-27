using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Identity.Contracts.v1.Impersonation;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;

namespace Trislab.Modules.Identity.Features.v1.Impersonation;

/// <summary>What the impersonation request handlers share: the caller, the not-found answer and the DTO.</summary>
internal static class ImpersonationRequests
{
    public static (string UserId, string TenantId) Caller(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var tenantId = currentUser.GetTenant()
            ?? throw new UnauthorizedException("missing tenant context").Localized("Error.InvalidTenant");
        return (currentUser.GetUserId().ToString(), tenantId);
    }

    /// <summary>404 for a request that doesn't exist and for one the caller may not see alike.</summary>
    public static NotFoundException NotFound() =>
        (NotFoundException)new NotFoundException("impersonation request not found")
            .Localized<IdentityResources>("Identity.ImpersonationRequestNotFound");

    public static ImpersonationRequestDto ToDto(ImpersonationRequest r, DateTime nowUtc) => new(
        Id: r.Id,
        ActorUserId: r.ActorUserId,
        ActorUserName: r.ActorUserName,
        ActorTenantId: r.ActorTenantId,
        TargetUserId: r.TargetUserId,
        TargetUserName: r.TargetUserName,
        TargetTenantId: r.TargetTenantId,
        Reason: r.Reason,
        RequestedAtUtc: r.RequestedAtUtc,
        ExpiresAtUtc: r.ExpiresAtUtc,
        AnsweredAtUtc: r.AnsweredAtUtc,
        GrantId: r.GrantId,
        Status: r.StatusAt(nowUtc));
}
