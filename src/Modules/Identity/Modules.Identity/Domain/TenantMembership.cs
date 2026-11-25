using Trislab.Framework.Core.Domain;
using Trislab.Modules.Identity.Domain.Events;

namespace Trislab.Modules.Identity.Domain;

/// <summary>
/// A user's membership in one tenant. Users are global (one account per e-mail address); a membership
/// gives the account access to a tenant, where its roles, groups and sessions live. Deactivating a user
/// in a tenant deactivates this membership, never the account, so the user keeps their other tenants.
/// </summary>
/// <remarks>
/// Tenant-scoped like every other entity: the tenant filter shows only the current tenant's
/// memberships. Sign-in reads a user's memberships across tenants with <c>IgnoreQueryFilters()</c>.
/// A member of the root tenant (an operator) has no other membership; operators reach other tenants
/// only through impersonation.
/// </remarks>
public class TenantMembership : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public string TenantId { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }

    public virtual TrislabUser? User { get; init; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private TenantMembership() { } // EF Core

    public static TenantMembership Create(string userId, string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return new TenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            IsActive = true,
            CreatedOnUtc = TimeProvider.System.GetUtcNow().UtcDateTime,
        };
    }

    /// <summary>Activates the membership and records <see cref="UserActivatedEvent"/>.</summary>
    public void Activate(string? activatedBy = null)
    {
        if (IsActive) return;
        IsActive = true;
        _domainEvents.Add(UserActivatedEvent.Create(UserId, activatedBy, tenantId: TenantId));
    }

    /// <summary>Deactivates the membership and records <see cref="UserDeactivatedEvent"/>.</summary>
    public void Deactivate(string? deactivatedBy = null, string? reason = null)
    {
        if (!IsActive) return;
        IsActive = false;
        _domainEvents.Add(UserDeactivatedEvent.Create(UserId, deactivatedBy, reason, tenantId: TenantId));
    }
}
