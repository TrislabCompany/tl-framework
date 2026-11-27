using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Trislab.Framework.Core.Domain;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Identity.Data;

/// <summary>
/// The Identity store. Users are global: one account per e-mail address, with its password, second
/// factor, claims, logins and password history. Everything a tenant grants is tenant-scoped: the
/// <see cref="TenantMembership"/>, roles and their claims, user roles, groups and sessions.
/// </summary>
/// <remarks>
/// <para>Identity always uses the main database, never a tenant's own connection string: a global
/// user and the memberships that point at it must live in one place.</para>
/// <para>The query filter <see cref="MembershipFilter"/> limits <c>Users</c> to the members of the
/// current tenant (active or not), so <c>UserManager</c> and every user query stay tenant-isolated as
/// before. Code that works on the account itself (sign-in, password reset, e-mail confirmation,
/// registration of an e-mail that already has an account) uses
/// <c>IgnoreQueryFilters([IdentityDbContext.MembershipFilter])</c>.</para>
/// </remarks>
public class IdentityDbContext : IdentityDbContext<TrislabUser,
    TrislabRole,
    string,
    IdentityUserClaim<string>,
    IdentityUserRole<string>,
    IdentityUserLogin<string>,
    TrislabRoleClaim,
    IdentityUserToken<string>,
    IdentityUserPasskey<string>>, IMultiTenantDbContext<string>
{
    /// <summary>Name of the query filter that limits users to the members of the current tenant who accepted (active or not); pending invitees stay invisible.</summary>
    public const string MembershipFilter = "TenantMembership";

    /// <summary>
    /// Entities of the global user account, never tenant-scoped though they don't carry <see cref="IGlobalEntity"/>
    /// (they are ASP.NET Identity's own types). Every other entity of this context is tenant-scoped unless it is an
    /// <see cref="IGlobalEntity"/>. <c>IdentityGlobalEntitiesTests</c> holds the model to exactly this rule.
    /// </summary>
    public static readonly IReadOnlyList<Type> AccountTypes =
    [
        typeof(TrislabUser),
        typeof(IdentityUserClaim<string>),
        typeof(IdentityUserLogin<string>),
        typeof(IdentityUserToken<string>),
    ];

    public IdentityDbContext(
        ITenantAccessor multiTenantContextAccessor,
        DbContextOptions<IdentityDbContext> options) : base(options)
    {
        ArgumentNullException.ThrowIfNull(multiTenantContextAccessor);
        TenantInfo = multiTenantContextAccessor.TenantInfo;
    }

    public ITenantInfo<string>? TenantInfo { get; set; }

    public TenantMismatchMode TenantMismatchMode { get; set; } = TenantMismatchMode.Throw;

    public TenantNotSetMode TenantNotSetMode { get; set; } = TenantNotSetMode.Throw;

    /// <summary>The tenant this context was created for, or null outside a tenant (sign-in, refresh).</summary>
    public string? CurrentTenantId => TenantInfo?.Id;

    /// <summary>
    /// The account with this id whatever the current tenant, or none: for work on the account itself (its password,
    /// its e-mail), which a reset link or a sign-in reaches without a membership in the request's tenant.
    /// </summary>
    public Task<TrislabUser?> FindAccountAsync(string userId, CancellationToken cancellationToken = default) =>
        Users.IgnoreQueryFilters([MembershipFilter]).SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    public DbSet<MembershipInvitation> MembershipInvitations => Set<MembershipInvitation>();

    public DbSet<SignInSelection> SignInSelections => Set<SignInSelection>();

    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupRole> GroupRoles => Set<GroupRole>();

    public DbSet<UserGroup> UserGroups => Set<UserGroup>();

    public DbSet<ImpersonationGrant> ImpersonationGrants => Set<ImpersonationGrant>();

    public DbSet<ImpersonationRequest> ImpersonationRequests => Set<ImpersonationRequest>();

    /// <summary>The active members of the current tenant.</summary>
    public IQueryable<TrislabUser> ActiveMembers =>
        Users.Where(u => TenantMemberships.Any(m => m.UserId == u.Id && m.IsActive && m.AcceptedOnUtc != null));

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // The outbox/inbox tables are framework infrastructure, owned by EventingDbContext (issue #1349).

        // Default-on tenant isolation, as ApplyTenantIsolationByDefault does it, except for the entities of
        // the global user account. The tenant-scoped Identity tables are marked IsMultiTenant in their configs.
        foreach (var entityType in builder.Model.GetEntityTypes().ToList())
        {
            if (entityType.IsOwned() || entityType.ClrType is null) continue;
            if (entityType.FindPrimaryKey() is null) continue;
            if (AccountTypes.Contains(entityType.ClrType)) continue;
            if (typeof(IGlobalEntity).IsAssignableFrom(entityType.ClrType)) continue;
            if (entityType.FindAnnotation(Finbuckle.MultiTenant.EntityFrameworkCore.Constants.MultiTenantAnnotationName) is not null) continue;

            builder.Entity(entityType.ClrType).IsMultiTenant<string>().AdjustUniqueIndexes();
        }

        builder.ConfigureMultiTenant<string>();

        // A user is visible in a tenant only through a membership there. TenantMemberships carries the tenant
        // filter itself; the explicit comparison keeps this filter correct on its own.
        builder.Entity<TrislabUser>().HasQueryFilter(
            MembershipFilter,
            u => TenantMemberships.Any(m => m.UserId == u.Id && m.TenantId == CurrentTenantId && m.AcceptedOnUtc != null));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.EnforceMultiTenant<IdentityDbContext, string>();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        this.EnforceMultiTenant<IdentityDbContext, string>();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
    }
}
