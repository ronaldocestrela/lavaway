using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class SubscriptionVehiclePlateConfiguration : IEntityTypeConfiguration<SubscriptionVehiclePlate>
{
    public void Configure(EntityTypeBuilder<SubscriptionVehiclePlate> builder)
    {
        builder.ToTable("SubscriptionVehiclePlates", "billing");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.Property(p => p.CustomerSubscriptionId)
            .IsRequired();

        builder.Property(p => p.Plate)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.AddedAtUtc)
            .IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.Plate });
        builder.HasIndex(p => new { p.TenantId, p.CustomerSubscriptionId });
    }
}
