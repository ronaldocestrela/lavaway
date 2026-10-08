using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class TenantPaymentGatewayConfigConfiguration : IEntityTypeConfiguration<TenantPaymentGatewayConfig>
{
    public void Configure(EntityTypeBuilder<TenantPaymentGatewayConfig> builder)
    {
        builder.ToTable("TenantPaymentGatewayConfigs", "billing");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.PagarMeSecretKeyEncrypted)
            .HasMaxLength(1000);

        builder.Property(c => c.PagarMePublicKey)
            .HasMaxLength(200);

        builder.Property(c => c.PagarMeWebhookSecretEncrypted)
            .HasMaxLength(1000);

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        builder.Property(c => c.LastTestMessage)
            .HasMaxLength(500);

        builder.HasIndex(c => c.TenantId)
            .IsUnique();
    }
}
