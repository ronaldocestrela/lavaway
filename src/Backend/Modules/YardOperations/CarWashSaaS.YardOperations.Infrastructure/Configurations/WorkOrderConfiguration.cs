using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders", "yard");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.Property(order => order.TenantId).IsRequired();
        builder.Property(order => order.CustomerId).IsRequired();
        builder.Property(order => order.VehicleId).IsRequired();
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(order => order.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(order => order.Notes).HasMaxLength(500);
        builder.Ignore(order => order.TotalAmount);
        builder.Ignore(order => order.EstimatedDurationMinutes);
        builder.Ignore(order => order.EstimatedCompletionAtUtc);
        builder.HasAlternateKey(order => new { order.TenantId, order.Id });
        builder.HasIndex(order => new { order.TenantId, order.CreatedAtUtc });
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(order => new { order.TenantId, order.CustomerId })
            .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(order => new { order.TenantId, order.VehicleId })
            .HasPrincipalKey(vehicle => new { vehicle.TenantId, vehicle.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(order => order.Items, item =>
        {
            item.ToTable("WorkOrderItems", "yard");
            item.WithOwner().HasForeignKey("WorkOrderId");
            item.HasKey(value => value.Id);
            item.Property(value => value.Id).ValueGeneratedNever();
            item.Property(value => value.TenantId).IsRequired();
            item.Property(value => value.ServiceId).IsRequired();
            item.Property(value => value.ServiceName).HasMaxLength(200).IsRequired();
            item.Property(value => value.UnitPrice).HasPrecision(18, 2).IsRequired();
            item.Property(value => value.EstimatedDurationMinutes).IsRequired();
            item.Property(value => value.Quantity).IsRequired();
            item.Ignore(value => value.TotalAmount);
            item.Ignore(value => value.TotalDurationMinutes);
            item.HasIndex(value => new { value.TenantId, value.ServiceId });
        });

        builder.Navigation(order => order.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
