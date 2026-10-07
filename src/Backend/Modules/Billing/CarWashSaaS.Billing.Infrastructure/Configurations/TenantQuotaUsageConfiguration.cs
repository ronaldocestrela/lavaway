using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class TenantQuotaUsageConfiguration : IEntityTypeConfiguration<TenantQuotaUsage>
{
    public void Configure(EntityTypeBuilder<TenantQuotaUsage> builder)
    {
        builder.ToTable("TenantQuotaUsages", "billing");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.TenantId)
            .IsRequired();

        builder.HasIndex(q => q.TenantId);

        builder.Property(q => q.CycleStartUtc)
            .IsRequired();

        builder.Property(q => q.CycleEndUtc)
            .IsRequired();

        builder.Property(q => q.WorkOrdersCreatedCount)
            .IsRequired();

        builder.Property(q => q.WhatsAppMessagesSentCount)
            .IsRequired();

        builder.Property(q => q.LastUpdatedAtUtc)
            .IsRequired();
    }
}
