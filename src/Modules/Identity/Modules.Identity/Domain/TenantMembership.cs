using Trislab.Framework.Core.Domain;
using Trislab.Modules.Identity.Domain.Events;

namespace Trislab.Modules.Identity.Domain;

/// <summary>
/// A user's membership in one tenant. Users are global (one account per e-mail address); a membership
/// gives the account access to a tenant, where its roles, groups and sessions live. Deactivating a user
/// in a tenant deactivates this membership, never the account, so the user keeps their other tenants.
/// </summary>
/// <remarks>
/// <para>Tenant-scoped like every other entity: the tenant filter shows only the current tenant's
/// memberships. Sign-in reads a user's memberships across tenants with <c>IgnoreQueryFilters()</c>.
/// A member of the root tenant (an operator) has no other membership; operators reach other tenants
/// only through impersonation.</para>
/// <para>A membership an administrator offers is <see cref="IsPending"/> until the account's owner accepts
/// the e-mailed invitation. Nobody else can activate it, so an administrator can never put an account
/// into a tenant against its owner's will, nor learn anything about it before the owner accepts.</para>
/// </remarks>
public class TenantMembership : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public string TenantId { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }

    /// <summary>When the account's owner accepted the membership; null while an invitation is pending.</summary>
    public DateTime? AcceptedOnUtc { get; private set; }

    /// <summary>Who invited the account (a user id), or null when the owner joined on their own.</summary>
    public string? InvitedBy { get; private set; }

    /// <summary>True while the owner has not accepted the invitation. A pending member has no access.</summary>
    public bool IsPending => AcceptedOnUtc is null;

    public virtual TrislabUser? User { get; init; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private TenantMembership() { } // EF Core

    /// <summary>
    /// A membership its owner holds already: the owner signed up in this tenant themselves, or the platform
    /// seeded the tenant's first administrator.
    /// </summary>
    public static TenantMembership CreateAccepted(string userId, string tenantId)
    {
        var membership = New(userId, tenantId, invitedBy: null);
        membership.IsActive = true;
        membership.AcceptedOnUtc = membership.CreatedOnUtc;
        return membership;
    }

    /// <summary>A membership an administrator offers: no access until the account's owner accepts the invitation.</summary>
    public static TenantMembership CreatePending(string userId, string tenantId, string invitedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invitedBy);
        return New(userId, tenantId, invitedBy);
    }

    private static TenantMembership New(string userId, string tenantId, string? invitedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return new TenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            IsActive = false,
            InvitedBy = invitedBy,
            CreatedOnUtc = TimeProvider.System.GetUtcNow().UtcDateTime,
        };
    }

    /// <summary>The owner accepts the invitation: the membership becomes active.</summary>
    public void Accept(DateTime acceptedOnUtc)
    {
        if (!IsPending) return;
        AcceptedOnUtc = acceptedOnUtc;
        IsActive = true;
        _domainEvents.Add(UserActivatedEvent.Create(UserId, UserId, tenantId: TenantId));
    }

    /// <summary>
    /// Activates the membership and records <see cref="UserActivatedEvent"/>. A pending membership can't be
    /// activated this way: only its owner's acceptance activates it.
    /// </summary>
    public void Activate(string? activatedBy = null)
    {
        if (IsPending)
        {
            throw new InvalidOperationException("A pending membership is activated only by its owner's acceptance.");
        }

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
