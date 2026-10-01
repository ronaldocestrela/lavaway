using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", "yard");
        builder.HasKey(vehicle => vehicle.Id);
        builder.Property(vehicle => vehicle.Id).ValueGeneratedNever();
        builder.Property(vehicle => vehicle.TenantId).IsRequired();
        builder.Property(vehicle => vehicle.CustomerId).IsRequired();
        builder.Property(vehicle => vehicle.Plate).HasMaxLength(7).IsUnicode(false).IsRequired();
        builder.Property(vehicle => vehicle.Size).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasAlternateKey(vehicle => new { vehicle.TenantId, vehicle.Id });
        builder.HasIndex(vehicle => new { vehicle.TenantId, vehicle.Plate }).IsUnique();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(vehicle => new { vehicle.TenantId, vehicle.CustomerId })
            .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}