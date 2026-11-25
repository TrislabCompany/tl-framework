using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Trislab.Modules.Identity.Data;

internal sealed class IdentityDbInitializer(
    ILogger<IdentityDbInitializer> logger,
    IdentityDbContext context,
    RoleManager<TrislabRole> roleManager,
    UserManager<TrislabUser> userManager,
    TimeProvider timeProvider,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    ITenantInitialPasswordBuffer passwordBuffer,
    IConfiguration configuration) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[{Tenant}] applied database migrations for identity module", context.TenantInfo?.Identifier);
            }
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedRolesAsync(cancellationToken);
        await SeedSystemGroupsAsync(cancellationToken);
        await SeedAdminUserAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken = default)
    {
        foreach (string roleName in RoleConstants.DefaultRoles)
        {
            if (await roleManager.Roles.SingleOrDefaultAsync(r => r.Name == roleName, cancellationToken)
                is not TrislabRole role)
            {
                // create role
                role = new TrislabRole(roleName, $"{roleName} Role for {multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id} Tenant");
                await roleManager.CreateAsync(role);
            }

            // Assign permissions
            if (roleName == RoleConstants.Basic)
            {
                await AssignPermissionsToRoleAsync(context, PermissionConstants.Basic, role, cancellationToken);
            }
            else if (roleName == RoleConstants.Admin)
            {
                await AssignPermissionsToRoleAsync(context, PermissionConstants.Admin, role, cancellationToken);

                if (multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id == MultitenancyConstants.Root.Id)
                {
                    await AssignPermissionsToRoleAsync(context, PermissionConstants.Root, role, cancellationToken);
                }
            }
        }
    }

    private async Task AssignPermissionsToRoleAsync(IdentityDbContext dbContext, IReadOnlyList<TrislabPermission> permissions, TrislabRole role, CancellationToken cancellationToken = default)
    {
        var currentClaims = await roleManager.GetClaimsAsync(role);
        var newClaims = permissions
            .Where(permission => !currentClaims.Any(c => c.Type == ClaimConstants.Permission && c.Value == permission.Name))
            .Select(permission => new TrislabRoleClaim
            {
                RoleId = role.Id,
                ClaimType = ClaimConstants.Permission,
                ClaimValue = permission.Name,
                CreatedBy = "application",
                CreatedOn = timeProvider.GetUtcNow()
            })
            .ToList();

        foreach (var claim in newClaims)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Seeding {Role} Permission '{Permission}' for '{TenantId}' Tenant.", role.Name, claim.ClaimValue, multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            }
            await dbContext.RoleClaims.AddAsync(claim, cancellationToken);
        }

        if (newClaims.Count == 0)
        {
            return;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }
        catch (DbUpdateException ex) when (RoleClaimUniqueness.IsDuplicateRoleClaim(ex))
        {
            // Another writer (the startup permission sync or a second instance) inserted the same grant
            // first, so the role already holds it. Drop the rejected entries so later saves don't retry them.
            dbContext.ChangeTracker.Clear();
        }

        logger.LogRoleClaimsAlreadyInserted(role.Name, multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
    }

    private async Task SeedSystemGroupsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        // Seed "All Users" default group - all new users are automatically added to this group
        const string allUsersGroupName = "All Users";
        var allUsersGroup = await context.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Name == allUsersGroupName && g.IsSystemGroup, cancellationToken);

        if (allUsersGroup is null)
        {
            allUsersGroup = Group.Create(
                name: allUsersGroupName,
                description: "Default group for all users. New users are automatically added to this group.",
                isDefault: true,
                isSystemGroup: true,
                createdBy: "System");

            await context.Groups.AddAsync(allUsersGroup, cancellationToken);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Seeding '{GroupName}' system group for '{TenantId}' Tenant.", allUsersGroupName, tenantId);
            }
        }

        // Seed "Administrators" group with Admin role
        const string administratorsGroupName = "Administrators";
        var administratorsGroup = await context.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Name == administratorsGroupName && g.IsSystemGroup, cancellationToken);

        if (administratorsGroup is null)
        {
            administratorsGroup = Group.Create(
                name: administratorsGroupName,
                description: "System group for administrators with full administrative privileges.",
                isDefault: false,
                isSystemGroup: true,
                createdBy: "System");

            await context.Groups.AddAsync(administratorsGroup, cancellationToken);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Seeding '{GroupName}' system group for '{TenantId}' Tenant.", administratorsGroupName, tenantId);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Assign Admin role to Administrators group
        var adminRole = await roleManager.FindByNameAsync(RoleConstants.Admin);
        if (adminRole is not null)
        {
            var existingGroupRole = await context.GroupRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(gr => gr.GroupId == administratorsGroup.Id && gr.RoleId == adminRole.Id, cancellationToken);

            if (existingGroupRole is null)
            {
                context.GroupRoles.Add(GroupRole.Create(administratorsGroup.Id, adminRole.Id));

                await context.SaveChangesAsync(cancellationToken);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Assigned Admin role to '{GroupName}' group for '{TenantId}' Tenant.", administratorsGroupName, tenantId);
                }
            }
        }
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id) || string.IsNullOrWhiteSpace(multiTenantContextAccessor.MultiTenantContext.TenantInfo?.AdminEmail))
        {
            return;
        }

        var tenantId = multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id;

        // Accounts are global: the admin e-mail may already have an account (the same person runs another
        // tenant). Then that account joins this tenant with its own password, and no new account is made.
        if (await userManager.FindByEmailAsync(multiTenantContextAccessor.MultiTenantContext.TenantInfo.AdminEmail.ToUpperInvariant())
            is not TrislabUser adminUser)
        {
            var initialPassword = ResolveInitialAdminPassword(multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id);
            if (initialPassword is null)
            {
                // No password (SEED_ADMIN_PASSWORD in a stamp): no admin account rather than one with a guessable password.
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("No initial admin password for '{TenantId}' Tenant, so no admin user is seeded.", multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
                }
                return;
            }

            string adminUserName = $"{multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id.Trim()}.{RoleConstants.Admin}".ToUpperInvariant();
            adminUser = new TrislabUser
            {
                FirstName = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id.Trim().ToUpperInvariant(),
                LastName = RoleConstants.Admin,
                Email = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.AdminEmail,
                UserName = adminUserName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.AdminEmail.ToUpperInvariant(),
                NormalizedUserName = adminUserName.ToUpperInvariant(),
                // No default avatar: the asset was never shipped, and baking an absolute
                // {OriginUrl}/… URL at seed time pinned it to the seeder's localhost origin
                // (migrator has no OriginOptions). Leave null → the SPA renders initials.
                ImageUrl = null,
            };

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Seeding Default Admin User for '{TenantId}' Tenant.", multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            }
            var password = new PasswordHasher<TrislabUser>();
            adminUser.PasswordHash = password.HashPassword(adminUser, initialPassword);
            // MUST check IdentityResult: a silent failure (password-policy reject, transient DB error)
            // would mark provisioning "Completed" with no admin user; throwing makes it a retryable Failed.
            var createResult = await userManager.CreateAsync(adminUser);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed admin user for tenant '{multiTenantContextAccessor.MultiTenantContext.TenantInfo!.Id}': "
                    + string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }
        }

        await EnsureMembershipAsync(adminUser.Id, tenantId, cancellationToken);

        // Assign role to user
        if (!await userManager.IsInRoleAsync(adminUser, RoleConstants.Admin))
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Assigning Admin Role to Admin User for '{TenantId}' Tenant.", multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            }
            await userManager.AddToRoleAsync(adminUser, RoleConstants.Admin);
        }
    }

    private async Task EnsureMembershipAsync(string userId, string tenantId, CancellationToken cancellationToken)
    {
        var tenants = await context.TenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .Select(m => m.TenantId)
            .ToListAsync(cancellationToken);

        if (tenants.Contains(tenantId))
        {
            return;
        }

        // Operators stay in root: an operator's e-mail can't be a tenant's admin, and the reverse.
        var root = MultitenancyConstants.Root.Id;
        if (tenants.Count > 0 && (string.Equals(tenantId, root, StringComparison.Ordinal) || tenants.Contains(root)))
        {
            throw new InvalidOperationException(
                $"The admin e-mail of tenant '{tenantId}' belongs to an account of another tenant, and operator accounts stay in the root tenant only.");
        }

        context.TenantMemberships.Add(TenantMembership.Create(userId, tenantId));
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Resolve the initial password for the admin user being seeded into a tenant.
    /// Lookup order:
    ///   1. <see cref="ITenantInitialPasswordBuffer"/> — set by <c>CreateTenantCommandHandler</c>
    ///      for runtime-created tenants (atomic consume, gone after this call).
    ///   2. <c>Seed:DefaultAdminPassword</c> from configuration — covers the framework's
    ///      root-tenant seed at startup and any test-host bootstrap. Operators set this
    ///      via env var / user-secrets / production secrets manager.
    /// Returns null if neither source supplies a password, and no admin user is seeded —
    /// safer than minting an admin user with a predictable secret.
    /// </summary>
    private string? ResolveInitialAdminPassword(string tenantId)
    {
        var buffered = passwordBuffer.TryConsume(tenantId);
        if (!string.IsNullOrWhiteSpace(buffered))
        {
            return buffered;
        }

        var fromConfig = configuration["Seed:DefaultAdminPassword"];
        return string.IsNullOrWhiteSpace(fromConfig) ? null : fromConfig;
    }
}