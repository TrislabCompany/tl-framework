using Trislab.Framework.Core.Domain;
using Trislab.Modules.Identity.Contracts.v1.Impersonation;

namespace Trislab.Modules.Identity.Domain;

/// <summary>
/// An operator's request to impersonate one user, which that user approves or denies (D-44). Only an approved,
/// unexpired request starts an impersonation, once; the user can deny it at any time, which also ends the
/// impersonation it started. Every state change is a conditional update in its handler (approve, deny, start),
/// so concurrent answers and starts can't overwrite each other.
/// </summary>
/// <remarks>
/// <see cref="IGlobalEntity"/>: the actor and the target may be in different tenants, so the tenant filter would
/// hide the row from one of them. Every query filters on the actor or the target explicitly.
/// </remarks>
public class ImpersonationRequest : IGlobalEntity
{
    /// <summary>How long a request can wait for an answer, and an approval for its start.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public Guid Id { get; private set; }

    public string ActorUserId { get; private set; } = default!;
    public string? ActorUserName { get; private set; }
    public string ActorTenantId { get; private set; } = default!;

    public string TargetUserId { get; private set; } = default!;
    public string? TargetUserName { get; private set; }
    public string TargetTenantId { get; private set; } = default!;

    public string Reason { get; private set; } = string.Empty;

    public DateTime RequestedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public ImpersonationRequestState State { get; private set; }
#pragma warning disable S1144 // Set by EF: the handlers change state only with conditional ExecuteUpdate calls.
    public DateTime? AnsweredAtUtc { get; private set; }

    /// <summary>The impersonation this request started, once it was used.</summary>
    public Guid? GrantId { get; private set; }
#pragma warning restore S1144

    private ImpersonationRequest() { } // EF Core

    public static ImpersonationRequest Create(
        string actorUserId,
        string? actorUserName,
        string actorTenantId,
        string targetUserId,
        string? targetUserName,
        string targetTenantId,
        string reason,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new ImpersonationRequest
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            ActorUserName = actorUserName,
            ActorTenantId = actorTenantId,
            TargetUserId = targetUserId,
            TargetUserName = targetUserName,
            TargetTenantId = targetTenantId,
            Reason = reason,
            RequestedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(Lifetime),
            State = ImpersonationRequestState.Pending,
        };
    }

    public ImpersonationRequestStatus StatusAt(DateTime nowUtc) => State switch
    {
        ImpersonationRequestState.Denied => ImpersonationRequestStatus.Denied,
        ImpersonationRequestState.Used => ImpersonationRequestStatus.Used,
        _ when nowUtc >= ExpiresAtUtc => ImpersonationRequestStatus.Expired,
        ImpersonationRequestState.Approved => ImpersonationRequestStatus.Approved,
        _ => ImpersonationRequestStatus.Pending,
    };
}

/// <summary>The stored state; expiry is computed (<see cref="ImpersonationRequest.StatusAt"/>).</summary>
public enum ImpersonationRequestState
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
    Used = 3,
}
