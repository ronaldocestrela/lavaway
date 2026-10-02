using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services", "yard");
        builder.HasKey(service => service.Id);
        builder.Property(service => service.Id).ValueGeneratedNever();
        builder.Property(service => service.TenantId).IsRequired();
        builder.Property(service => service.Name).HasMaxLength(200).IsRequired();
        builder.Property(service => service.Category).HasMaxLength(100).IsRequired();
        builder.HasAlternateKey(service => new { service.TenantId, service.Id });

        builder.OwnsMany(service => service.Prices, price =>
        {
            price.ToTable("ServicePrices", "yard");
            price.WithOwner().HasForeignKey("ServiceId");
            price.HasKey("ServiceId", nameof(ServicePrice.VehicleSize));
            price.Property(value => value.TenantId).IsRequired();
            price.Property(value => value.VehicleSize).HasConversion<string>().HasMaxLength(32).IsRequired();
            price.Property(value => value.Amount).HasPrecision(18, 2).IsRequired();
            price.Property(value => value.EstimatedDurationMinutes).IsRequired();
            price.HasIndex(value => new { value.TenantId, value.VehicleSize });
        });

        builder.Navigation(service => service.Prices).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
