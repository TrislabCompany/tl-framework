using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Trislab.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trislab.Modules.Identity.Data;

public class ApplicationUserConfig : IEntityTypeConfiguration<TrislabUser>
{
    public void Configure(EntityTypeBuilder<TrislabUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Users are global: one account per e-mail address across all tenants. Tenant access is a
        // TenantMembership; IdentityDbContext adds the query filter that limits users to the members
        // of the current tenant.
        builder.ToTable("Users", IdentityModuleConstants.SchemaName);

        // Identity enforces RequireUniqueEmail in its validator; the index makes it hold under races too.
        builder
            .HasIndex(u => u.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("EmailIndex");

        builder
            .Property(u => u.DefaultTenantId)
                .HasMaxLength(64);

        builder
            .Property(u => u.ObjectId)
                .HasMaxLength(256);

        // A BCP-47 tag is short and bounded; 10 covers language-script-region (zh-Hant-TW). Writes are
        // additionally constrained to SupportedCultures.Tags by UpdateUserCommandValidator, so this is
        // the storage-level backstop, not the validation.
        builder
            .Property(u => u.Locale)
                .HasMaxLength(10);
    }
}

public class ApplicationRoleConfig : IEntityTypeConfiguration<TrislabRole>
{
    public void Configure(EntityTypeBuilder<TrislabRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .ToTable("Roles", IdentityModuleConstants.SchemaName)
            .IsMultiTenant()
                .AdjustUniqueIndexes();
    }
}

public class ApplicationRoleClaimConfig : IEntityTypeConfiguration<TrislabRoleClaim>
{
    public void Configure(EntityTypeBuilder<TrislabRoleClaim> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .ToTable("RoleClaims", IdentityModuleConstants.SchemaName)
            .IsMultiTenant();

        // One row per grant. ClaimValue is nullable and PostgreSQL treats NULLs as distinct here,
        // which is fine: a null-valued claim grants nothing and the permission sync removes it.
        builder
            .HasIndex(rc => new { rc.RoleId, rc.ClaimType, rc.ClaimValue })
            .IsUnique()
            .HasDatabaseName(RoleClaimUniqueness.IndexName);
    }
}

public class IdentityUserRoleConfig : IEntityTypeConfiguration<IdentityUserRole<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<string>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .ToTable("UserRoles", IdentityModuleConstants.SchemaName)
            .IsMultiTenant();
    }
}

public class IdentityUserClaimConfig : IEntityTypeConfiguration<IdentityUserClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<string>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Belongs to the global user account (the authenticator key is a user token), so not tenant-scoped.
        builder.ToTable("UserClaims", IdentityModuleConstants.SchemaName);
    }
}

public class IdentityUserLoginConfig : IEntityTypeConfiguration<IdentityUserLogin<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Belongs to the global user account (the authenticator key is a user token), so not tenant-scoped.
        builder.ToTable("UserLogins", IdentityModuleConstants.SchemaName);
    }
}

public class IdentityUserTokenConfig : IEntityTypeConfiguration<IdentityUserToken<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<string>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Belongs to the global user account (the authenticator key is a user token), so not tenant-scoped.
        builder.ToTable("UserTokens", IdentityModuleConstants.SchemaName);
    }
}
public class TenantMembershipConfig : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .ToTable("TenantMemberships", IdentityModuleConstants.SchemaName)
            .IsMultiTenant();

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.UserId).IsRequired().HasMaxLength(450);
        builder.Property(m => m.TenantId).IsRequired().HasMaxLength(64);

        builder
            .HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.UserId, m.TenantId }).IsUnique();
        builder.HasIndex(m => m.TenantId);
    }
}
