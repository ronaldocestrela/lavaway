using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class SaasInvoiceConfiguration : IEntityTypeConfiguration<SaasInvoice>
{
    public void Configure(EntityTypeBuilder<SaasInvoice> builder)
    {
        builder.ToTable("SaasInvoices", "billing");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId)
            .IsRequired();

        builder.HasIndex(i => i.TenantId);

        builder.Property(i => i.GatewayInvoiceId)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(i => i.GatewayInvoiceId);

        builder.Property(i => i.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.DueDateUtc)
            .IsRequired();

        builder.Property(i => i.PaidAtUtc);

        builder.Property(i => i.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.PaymentUrl)
            .HasMaxLength(1000);

        builder.Property(i => i.PixQrCode)
            .HasMaxLength(2000);

        builder.Property(i => i.PixCopiaECola)
            .HasMaxLength(1000);

        builder.Property(i => i.CreatedAtUtc)
            .IsRequired();
    }
}
