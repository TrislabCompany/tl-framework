using Trislab.Modules.SubscriptionsUsage.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trislab.Modules.SubscriptionsUsage.Data.Configurations;

public sealed class UsageCounterConfiguration : IEntityTypeConfiguration<UsageCounter>
{
    public void Configure(EntityTypeBuilder<UsageCounter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("UsageCounters"); // also written out in UsageLimits' upsert
        builder.HasKey(x => new { x.TenantId, x.MeterKey, x.PeriodStart });
        builder.Property(x => x.TenantId).HasMaxLength(64);
        builder.Property(x => x.MeterKey).HasMaxLength(64);
        builder.Property(x => x.Used).IsRequired();
    }
}

