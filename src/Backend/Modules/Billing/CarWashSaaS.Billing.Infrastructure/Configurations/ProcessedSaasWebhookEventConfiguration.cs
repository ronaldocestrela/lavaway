using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class ProcessedSaasWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedSaasWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedSaasWebhookEvent> builder)
    {
        builder.ToTable("ProcessedSaasWebhookEvents", "billing");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventId)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(e => e.EventId)
            .IsUnique();

        builder.Property(e => e.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.ReceivedAtUtc)
            .IsRequired();
    }
}
