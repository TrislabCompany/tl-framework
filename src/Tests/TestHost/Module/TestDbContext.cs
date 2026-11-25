using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trislab.Framework.Persistence.Context;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Persistence;

namespace TestHost;

internal sealed class TestDbContext : BaseDbContext
{
    public const string Schema = "testhost";

    public TestDbContext(
        ITenantAccessor multiTenantContextAccessor,
        DbContextOptions<TestDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<TestItem> Items => Set<TestItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<TestItem>(b =>
        {
            b.ToTable("Items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.ImageUrl).HasMaxLength(512);
            b.HasIndex(x => x.FileAssetId);
            b.Ignore(x => x.DomainEvents);
        });

        // base.OnModelCreating runs last so tenant isolation sees the configured entity.
        base.OnModelCreating(modelBuilder);
    }
}
