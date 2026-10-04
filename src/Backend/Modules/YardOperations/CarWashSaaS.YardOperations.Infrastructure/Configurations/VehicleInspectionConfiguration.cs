using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class VehicleInspectionConfiguration : IEntityTypeConfiguration<VehicleInspection>
{
    public void Configure(EntityTypeBuilder<VehicleInspection> builder)
    {
        builder.ToTable("VehicleInspections", "yard");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.WorkOrderId).IsRequired();
        builder.Property(i => i.VehicleId).IsRequired();
        builder.Property(i => i.FuelLevel).HasMaxLength(32);
        builder.Property(i => i.OdometerKm);
        builder.Property(i => i.Notes).HasMaxLength(500);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(i => i.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(i => i.CompletedAtUtc).HasColumnType("datetimeoffset");

        builder.HasAlternateKey(i => new { i.TenantId, i.Id });
        builder.HasIndex(i => new { i.TenantId, i.WorkOrderId }).IsUnique();

        builder.OwnsMany(i => i.Damages, damage =>
        {
            damage.ToTable("InspectionDamages", "yard");
            damage.WithOwner().HasForeignKey("VehicleInspectionId");
            damage.HasKey(d => d.Id);
            damage.Property(d => d.Id).ValueGeneratedNever();
            damage.Property(d => d.TenantId).IsRequired();
            damage.Property(d => d.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            damage.Property(d => d.View).HasConversion<string>().HasMaxLength(32).IsRequired();
            damage.Property(d => d.CoordinateX).HasPrecision(5, 2).IsRequired();
            damage.Property(d => d.CoordinateY).HasPrecision(5, 2).IsRequired();
            damage.Property(d => d.Severity).HasConversion<string>().HasMaxLength(32).IsRequired();
            damage.Property(d => d.Description).HasMaxLength(300);
            damage.HasIndex(d => new { d.TenantId, d.VehicleInspectionId });
        });

        builder.OwnsMany(i => i.ChecklistItems, item =>
        {
            item.ToTable("InspectionChecklistItems", "yard");
            item.WithOwner().HasForeignKey("VehicleInspectionId");
            item.HasKey(c => c.Id);
            item.Property(c => c.Id).ValueGeneratedNever();
            item.Property(c => c.TenantId).IsRequired();
            item.Property(c => c.ItemKey).HasMaxLength(64).IsRequired();
            item.Property(c => c.Title).HasMaxLength(150).IsRequired();
            item.Property(c => c.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            item.Property(c => c.Observation).HasMaxLength(300);
            item.HasIndex(c => new { c.TenantId, c.VehicleInspectionId });
        });

        builder.OwnsMany(i => i.Photos, photo =>
        {
            photo.ToTable("InspectionPhotos", "yard");
            photo.WithOwner().HasForeignKey("VehicleInspectionId");
            photo.HasKey(p => p.Id);
            photo.Property(p => p.Id).ValueGeneratedNever();
            photo.Property(p => p.TenantId).IsRequired();
            photo.Property(p => p.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
            photo.Property(p => p.StoragePath).HasMaxLength(500).IsRequired();
            photo.Property(p => p.FileName).HasMaxLength(255).IsRequired();
            photo.Property(p => p.ContentType).HasMaxLength(64).IsRequired();
            photo.Property(p => p.SizeBytes).IsRequired();
            photo.Property(p => p.DamageId);
            photo.Property(p => p.UploadedAtUtc).HasColumnType("datetimeoffset").IsRequired();
            photo.HasIndex(p => new { p.TenantId, p.VehicleInspectionId });
        });

        builder.Navigation(i => i.Damages).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.ChecklistItems).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
