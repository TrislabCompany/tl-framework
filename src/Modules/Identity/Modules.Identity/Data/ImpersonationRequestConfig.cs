using Trislab.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trislab.Modules.Identity.Data;

public class ImpersonationRequestConfig : IEntityTypeConfiguration<ImpersonationRequest>
{
    public void Configure(EntityTypeBuilder<ImpersonationRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // NOT multitenant, like ImpersonationGrants: the actor and the target may be in different tenants.
        builder.ToTable("ImpersonationRequests", IdentityModuleConstants.SchemaName);

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ActorUserId).IsRequired().HasMaxLength(64);
        builder.Property(r => r.ActorUserName).HasMaxLength(256);
        builder.Property(r => r.ActorTenantId).IsRequired().HasMaxLength(64);

        builder.Property(r => r.TargetUserId).IsRequired().HasMaxLength(64);
        builder.Property(r => r.TargetUserName).HasMaxLength(256);
        builder.Property(r => r.TargetTenantId).IsRequired().HasMaxLength(64);

        builder.Property(r => r.Reason).IsRequired().HasMaxLength(500);
        builder.Property(r => r.State).HasConversion<string>().HasMaxLength(16);

        // "The requests waiting for me" and "the requests I made", newest first.
        builder.HasIndex(r => new { r.TargetTenantId, r.TargetUserId, r.RequestedAtUtc });
        builder.HasIndex(r => new { r.ActorUserId, r.RequestedAtUtc });
        builder.HasIndex(r => r.GrantId);
    }
}
