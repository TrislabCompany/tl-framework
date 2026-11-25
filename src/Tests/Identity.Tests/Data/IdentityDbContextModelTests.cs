using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Identity.Tests.Data;

public class IdentityDbContextModelTests
{
    private static IdentityDbContext CreateContext()
    {
        var accessor = Substitute.For<ITenantAccessor>();
        accessor.TenantInfo.Returns(new AppTenantInfo("test", "test"));

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=arch;Database=arch;Username=arch;Password=arch")
            .Options;

        return new IdentityDbContext(accessor, options);
    }

    // User.Locale holds a BCP-47 tag, which is short and bounded. Unbounded `text`
    // invites arbitrary input at the storage layer for a value the write boundary
    // already restricts to SupportedCultures.Tags. 10 covers the longest form the
    // platform could offer (language-script-region, e.g. zh-Hant-TW).
    [Fact]
    public void User_Locale_Is_Bounded_To_A_Bcp47_Tag_Length()
    {
        using var context = CreateContext();

        var locale = context.Model.FindEntityType(typeof(TrislabUser))?.FindProperty(nameof(TrislabUser.Locale));

        locale.ShouldNotBeNull();
        locale.GetMaxLength().ShouldBe(
            10,
            "an unbounded locale column accepts arbitrary input for a value that is always a short BCP-47 tag");
    }

    // Users are global: one account per e-mail across tenants, so the user table carries no tenant.
    [Fact]
    public void User_Is_Global_With_A_Unique_Email()
    {
        using var context = CreateContext();

        var user = context.Model.FindEntityType(typeof(TrislabUser))!;
        user.FindProperty("TenantId").ShouldBeNull("a user account belongs to no tenant");
        user.GetIndexes().Single(i => i.GetDatabaseName() == "EmailIndex").IsUnique.ShouldBeTrue();
        user.GetIndexes().Single(i => i.GetDatabaseName() == "UserNameIndex").IsUnique.ShouldBeTrue();
    }

    // Access to a tenant is a membership, and the membership is tenant-scoped.
    [Fact]
    public void Membership_Is_Tenant_Scoped_And_Unique_Per_User_And_Tenant()
    {
        using var context = CreateContext();

        var membership = context.Model.FindEntityType(typeof(TenantMembership))!;
        membership.FindProperty("TenantId").ShouldNotBeNull();
        membership.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(TenantMembership.UserId), nameof(TenantMembership.TenantId)]))
            .IsUnique.ShouldBeTrue();
    }
}
