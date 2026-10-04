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
        builder.Property(order => order.AssignedOperatorId);
        builder.Property(order => order.AssignedOperatorName).HasMaxLength(200);

        builder.Ignore(order => order.TotalAmount);
        builder.Ignore(order => order.EstimatedDurationMinutes);
        builder.Ignore(order => order.EstimatedCompletionAtUtc);

        builder.HasAlternateKey(order => new { order.TenantId, order.Id });
        builder.HasIndex(order => new { order.TenantId, order.CreatedAtUtc });
        builder.HasIndex(order => new { order.TenantId, order.Status });

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
            item.Property(value => value.ServiceCategory).HasMaxLength(100);
            item.Ignore(value => value.TotalAmount);
            item.Ignore(value => value.TotalDurationMinutes);
            item.HasIndex(value => new { value.TenantId, value.ServiceId });
        });

        builder.OwnsMany(order => order.StatusHistory, history =>
        {
            history.ToTable("WorkOrderStatusHistories", "yard");
            history.WithOwner().HasForeignKey("WorkOrderId");
            history.HasKey(h => h.Id);
            history.Property(h => h.Id).ValueGeneratedNever();
            history.Property(h => h.TenantId).IsRequired();
            history.Property(h => h.WorkOrderId).IsRequired();
            history.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(32);
            history.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
            history.Property(h => h.ChangedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            history.Property(h => h.ChangedByOperatorId);
            history.Property(h => h.ChangedByOperatorName).HasMaxLength(200);
            history.Property(h => h.Notes).HasMaxLength(500);
            history.HasIndex(h => new { h.TenantId, h.WorkOrderId });
        });

        builder.OwnsMany(order => order.PostServicePhotos, photo =>
        {
            photo.ToTable("WorkOrderPostServicePhotos", "yard");
            photo.WithOwner().HasForeignKey("WorkOrderId");
            photo.HasKey(p => p.Id);
            photo.Property(p => p.Id).ValueGeneratedNever();
            photo.Property(p => p.TenantId).IsRequired();
            photo.Property(p => p.WorkOrderId).IsRequired();
            photo.Property(p => p.WorkOrderItemId);
            photo.Property(p => p.BeforeInspectionPhotoId);
            photo.Property(p => p.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
            photo.Property(p => p.Title).HasMaxLength(150).IsRequired();
            photo.Property(p => p.StoragePath).HasMaxLength(500).IsRequired();
            photo.Property(p => p.FileName).HasMaxLength(255).IsRequired();
            photo.Property(p => p.ContentType).HasMaxLength(64).IsRequired();
            photo.Property(p => p.SizeBytes).IsRequired();
            photo.Property(p => p.UploadedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            photo.Property(p => p.Notes).HasMaxLength(500);
            photo.HasIndex(p => new { p.TenantId, p.WorkOrderId });
        });

        builder.Navigation(order => order.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(order => order.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(order => order.PostServicePhotos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
