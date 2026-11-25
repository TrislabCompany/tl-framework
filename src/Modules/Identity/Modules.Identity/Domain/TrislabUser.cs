using Trislab.Framework.Core.Domain;
using Trislab.Modules.Identity.Domain.Events;
using Microsoft.AspNetCore.Identity;

namespace Trislab.Modules.Identity.Domain;

/// <summary>
/// A user account. Accounts are global: one per e-mail address, with one password and one second
/// factor, whatever the number of tenants. Access to a tenant is a <see cref="TenantMembership"/>;
/// roles, groups and sessions belong to the membership's tenant.
/// </summary>
public class TrislabUser : IdentityUser, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public Uri? ImageUrl { get; set; }

    /// <summary>The tenant sign-in opens when the user has more than one. Null until the user picks one.</summary>
    public string? DefaultTenantId { get; set; }

    public virtual ICollection<TenantMembership> Memberships { get; set; } = new List<TenantMembership>();

    /// <summary>BCP 47 UI language tag (e.g. "sl-SI"); null resolves to the default culture.</summary>
    public string? Locale { get; set; }

    public string? ObjectId { get; set; }

    /// <summary>Timestamp when the user last changed their password</summary>
    public DateTime LastPasswordChangeDate { get; set; } = TimeProvider.System.GetUtcNow().UtcDateTime;

    // Navigation property for password history
    public virtual ICollection<PasswordHistory> PasswordHistories { get; set; } = new List<PasswordHistory>();

    // IHasDomainEvents implementation
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();
    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Records UserRegisteredEvent. Call after user creation.</summary>
    public void RecordRegistered(string? tenantId = null)
    {
        AddDomainEvent(UserRegisteredEvent.Create(
            userId: Id,
            email: Email ?? string.Empty,
            firstName: FirstName,
            lastName: LastName,
            tenantId: tenantId));
    }

    /// <summary>Records PasswordChangedEvent. Call after password change.</summary>
    public void RecordPasswordChanged(bool wasReset = false, string? tenantId = null)
    {
        AddDomainEvent(PasswordChangedEvent.Create(
            userId: Id,
            wasReset: wasReset,
            tenantId: tenantId));
    }

    /// <summary>Records UserRoleAssignedEvent. Call after roles are assigned.</summary>
    public void RecordRolesAssigned(IEnumerable<string> assignedRoles, string? tenantId = null)
    {
        var rolesList = assignedRoles.ToList();
        if (rolesList.Count == 0) return;
        AddDomainEvent(UserRoleAssignedEvent.Create(
            userId: Id,
            assignedRoles: rolesList,
            tenantId: tenantId));
    }
}