using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Trislab.Framework.Core.Domain;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Users are global, so Identity has entities without a tenant. The list is closed: every entity of
/// <see cref="IdentityDbContext"/> is tenant-scoped, except ASP.NET Identity's account types named in
/// <see cref="IdentityDbContext.AccountTypes"/> and entities marked <see cref="IGlobalEntity"/>.
/// A new global entity has to be one of those on purpose (database.md).
/// </summary>
public sealed class IdentityGlobalEntitiesTests
{
    private static readonly string[] ExpectedGlobal =
    [
        "TrislabUser",
        "IdentityUserClaim`1",
        "IdentityUserLogin`1",
        "IdentityUserToken`1",
        "PasswordHistory",
        "ImpersonationGrant",
        "ImpersonationRequest",
        "SignInSelection",
    ];

    [Fact]
    public void Only_The_Listed_Identity_Entities_Are_Global()
    {
        using var context = CreateContext();

        var global = context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.FindPrimaryKey() is not null && !e.IsMultiTenant)
            .Select(e => e.ClrType.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        global.ShouldBe(ExpectedGlobal.OrderBy(n => n, StringComparer.Ordinal).ToList(),
            "a global Identity entity is either an account type in IdentityDbContext.AccountTypes or an IGlobalEntity; anything else must be tenant-scoped");
    }

    [Fact]
    public void Every_Global_Identity_Entity_Is_An_Account_Type_Or_An_IGlobalEntity()
    {
        using var context = CreateContext();

        foreach (var entity in context.Model.GetEntityTypes().Where(e => !e.IsOwned() && e.FindPrimaryKey() is not null && !e.IsMultiTenant))
        {
            var allowed = IdentityDbContext.AccountTypes.Contains(entity.ClrType)
                || typeof(IGlobalEntity).IsAssignableFrom(entity.ClrType);
            allowed.ShouldBeTrue($"{entity.ClrType.Name} is global without being an account type or an IGlobalEntity");
        }
    }

    [Fact]
    public void Every_Account_Type_Is_In_The_Model()
    {
        using var context = CreateContext();

        foreach (var type in IdentityDbContext.AccountTypes)
        {
            context.Model.FindEntityType(type).ShouldNotBeNull(
                $"{type.Name} is listed as an account type but isn't mapped; drop it from IdentityDbContext.AccountTypes");
        }
    }

    // Bypassing every query filter also drops the tenant filter, so it reads across tenants. Only the classes below
    // do that, each on purpose (an account's memberships, sessions or invitations in every tenant); everything else
    // uses the named form, IgnoreQueryFilters([IdentityDbContext.MembershipFilter]), or stays in its tenant.
    private static readonly string[] CrossTenantReaders =
    [
        "Data/IdentityDbInitializer.cs",
        "Services/IdentityAuditor.cs",
        "Services/MembershipInvitationService.cs",
        "Services/MembershipRules.cs",
        "Services/SessionRevocation.cs",
        "Services/TenantSignInService.cs",
        "Services/UserCountQuotaGaugeProvider.cs",
    ];

    [Fact]
    public void Only_The_Listed_Identity_Classes_Bypass_Every_Query_Filter()
    {
        var root = Path.Combine(SolutionRoot(), "src", "Modules", "Identity", "Modules.Identity");
        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadLines(f).Any(l => l.Contains(".IgnoreQueryFilters()", StringComparison.Ordinal) && !l.TrimStart().StartsWith("///", StringComparison.Ordinal)))
            .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(f => !CrossTenantReaders.Contains(f, StringComparer.Ordinal))
            .ToList();

        offenders.ShouldBeEmpty(
            "these Identity files bypass every query filter, the tenant filter included; use IgnoreQueryFilters([IdentityDbContext.MembershipFilter]) or add the file to CrossTenantReaders with a reason");
    }

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Unable to locate the solution root.");
    }

    [Fact]
    public void Users_Are_Limited_To_The_Tenants_Members_By_A_Named_Filter()
    {
        using var context = CreateContext();

        var user = context.Model.FindEntityType(typeof(Trislab.Modules.Identity.Domain.TrislabUser))!;
        user.FindDeclaredQueryFilter(IdentityDbContext.MembershipFilter).ShouldNotBeNull();
    }

    private static IdentityDbContext CreateContext()
    {
        var accessor = new FixedTenant(new AppTenantInfo("t", "t"));
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=arch;Database=arch;Username=arch;Password=arch")
            .Options;
        return new IdentityDbContext(accessor, options);
    }

    private sealed class FixedTenant(AppTenantInfo tenant) : ITenantAccessor
    {
        public AppTenantInfo? TenantInfo => tenant;

        public bool IsResolved => true;

        public IDisposable Use(AppTenantInfo? tenant) => throw new NotSupportedException("The model tests use one tenant.");
    }
}
