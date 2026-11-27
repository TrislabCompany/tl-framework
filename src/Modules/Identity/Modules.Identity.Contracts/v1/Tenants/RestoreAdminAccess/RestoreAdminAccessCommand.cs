using System.Text.Json.Serialization;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Tenants.RestoreAdminAccess;

/// <summary>
/// A platform operator gives a tenant an Admin again, after checking with the customer (D-44): an active member with
/// that address becomes Admin, or the address is invited and becomes Admin when it accepts. Nothing else changes; the
/// tenant's active Admins are told by e-mail, and it is audited.
/// </summary>
public sealed record RestoreAdminAccessCommand(
    string TenantId,
    string Email,
    string Reason,
    string? FirstName = null,
    string? LastName = null)
    : ICommand<RestoreAdminAccessResponse>;

public sealed record RestoreAdminAccessResponse(RestoreAdminAccessOutcome Outcome, int AdminsNotified);

[JsonConverter(typeof(JsonStringEnumConverter<RestoreAdminAccessOutcome>))]
public enum RestoreAdminAccessOutcome
{
    /// <summary>The address belongs to an active member, who now holds the Admin role.</summary>
    RoleAssigned = 0,
    /// <summary>The address was invited; accepting the invitation makes it an Admin.</summary>
    Invited = 1,
}
