using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class SubscriptionUsageConfiguration : IEntityTypeConfiguration<SubscriptionUsage>
{
    public void Configure(EntityTypeBuilder<SubscriptionUsage> builder)
    {
        builder.ToTable("SubscriptionUsages", "billing");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.TenantId)
            .IsRequired();

        builder.Property(u => u.CustomerSubscriptionId)
            .IsRequired();

        builder.Property(u => u.WorkOrderId);

        builder.Property(u => u.Plate)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(u => u.ServiceName)
            .HasMaxLength(150);

        builder.Property(u => u.ConsumedAtUtc)
            .IsRequired();

        builder.Property(u => u.Notes)
            .HasMaxLength(300);

        builder.HasIndex(u => new { u.TenantId, u.WorkOrderId })
            .IsUnique()
            .HasFilter("[WorkOrderId] IS NOT NULL");

        builder.HasIndex(u => new { u.TenantId, u.CustomerSubscriptionId, u.ConsumedAtUtc });
        builder.HasIndex(u => new { u.TenantId, u.Plate });
    }
}
