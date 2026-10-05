using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class ProcessedPaymentWebhookConfiguration : IEntityTypeConfiguration<ProcessedPaymentWebhook>
{
    public void Configure(EntityTypeBuilder<ProcessedPaymentWebhook> builder)
    {
        builder.ToTable("ProcessedPaymentWebhooks", "billing");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.TenantId).IsRequired();
        builder.Property(w => w.Provider).HasMaxLength(64).IsRequired();
        builder.Property(w => w.EventId).HasMaxLength(128).IsRequired();
        builder.Property(w => w.TxId).HasMaxLength(128).IsRequired();
        builder.Property(w => w.Status).HasMaxLength(64).IsRequired();
        builder.Property(w => w.ReceivedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(w => w.ProcessedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(w => w.PayloadHash).HasMaxLength(128);
        builder.Property(w => w.Notes).HasMaxLength(500);

        builder.HasIndex(w => new { w.TenantId, w.Provider, w.EventId }).IsUnique();
        builder.HasIndex(w => new { w.TenantId, w.TxId });
    }
}
