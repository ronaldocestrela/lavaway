using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class PixChargeConfiguration : IEntityTypeConfiguration<PixCharge>
{
    public void Configure(EntityTypeBuilder<PixCharge> builder)
    {
        builder.ToTable("PixCharges", "billing");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.WorkOrderId)
            .IsRequired();

        builder.Property(c => c.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.TxId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.CopyPasteKey)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(c => c.QrCodeBase64)
            .IsRequired();

        builder.Property(c => c.ExpiresAtUtc)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.WorkOrderId });
        builder.HasIndex(c => new { c.TenantId, c.TxId });
    }
}
