using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", "yard");
        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id).ValueGeneratedNever();
        builder.Property(booking => booking.TenantId).IsRequired();
        builder.Property(booking => booking.Protocol).HasMaxLength(32).IsRequired();
        builder.Property(booking => booking.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(booking => booking.CustomerPhone).HasMaxLength(32).IsRequired();
        builder.Property(booking => booking.VehiclePlate).HasMaxLength(16).IsRequired();
        builder.Property(booking => booking.VehicleModel).HasMaxLength(100);
        builder.Property(booking => booking.VehicleSize).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(booking => booking.ServiceId).IsRequired();
        builder.Property(booking => booking.ServiceName).HasMaxLength(200).IsRequired();
        builder.Property(booking => booking.EstimatedPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(booking => booking.ScheduledDate).IsRequired();
        builder.Property(booking => booking.ScheduledTime).IsRequired();
        builder.Property(booking => booking.EstimatedDurationMinutes).IsRequired();
        builder.Property(booking => booking.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(booking => booking.Origin).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(booking => booking.WorkOrderId);
        builder.Property(booking => booking.Notes).HasMaxLength(500);
        builder.Property(booking => booking.CancellationReason).HasMaxLength(250);
        builder.Property(booking => booking.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(booking => booking.UpdatedAt).HasColumnType("datetimeoffset").IsRequired();

        builder.HasAlternateKey(booking => new { booking.TenantId, booking.Id });
        builder.HasIndex(booking => new { booking.TenantId, booking.Protocol }).IsUnique();
        builder.HasIndex(booking => new { booking.TenantId, booking.ScheduledDate, booking.ScheduledTime });
        builder.HasIndex(booking => new { booking.TenantId, booking.CustomerPhone });
        builder.HasIndex(booking => new { booking.TenantId, booking.Status });
    }
}
