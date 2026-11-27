using System.Text.Json.Serialization;

namespace Trislab.Modules.Identity.Contracts.v1.Impersonation;

/// <summary>An operator's request to impersonate a user, and the user's answer (D-44).</summary>
public sealed record ImpersonationRequestDto(
    Guid Id,
    string ActorUserId,
    string? ActorUserName,
    string ActorTenantId,
    string TargetUserId,
    string? TargetUserName,
    string TargetTenantId,
    string Reason,
    DateTime RequestedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? AnsweredAtUtc,
    Guid? GrantId,
    ImpersonationRequestStatus Status);

[JsonConverter(typeof(JsonStringEnumConverter<ImpersonationRequestStatus>))]
public enum ImpersonationRequestStatus
{
    /// <summary>Waiting for the user's answer.</summary>
    Pending = 0,
    /// <summary>The user approved it; the operator may start the impersonation once.</summary>
    Approved = 1,
    /// <summary>The user denied it, or took the approval back.</summary>
    Denied = 2,
    /// <summary>It started its impersonation (see the grant).</summary>
    Used = 3,
    /// <summary>No answer, or no start, within its lifetime.</summary>
    Expired = 4,
}
