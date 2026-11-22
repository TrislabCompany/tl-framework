using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Multitenancy.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trislab.Modules.Multitenancy.Data.Configurations;

public class TenantProvisioningStepConfiguration : IEntityTypeConfiguration<TenantProvisioningStep>
{
    public void Configure(EntityTypeBuilder<TenantProvisioningStep> builder)
    {
        builder.ToTable("TenantProvisioningSteps", MultitenancyConstants.Schema);
    }
}