using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class TenantSaasSubscriptionConfiguration : IEntityTypeConfiguration<TenantSaasSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSaasSubscription> builder)
    {
        builder.ToTable("TenantSaasSubscriptions", "billing");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.HasIndex(s => s.TenantId)
            .IsUnique();

        builder.Property(s => s.PlanTier)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired();

        builder.Property(s => s.MonthlyPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.CurrentPeriodStartUtc)
            .IsRequired();

        builder.Property(s => s.CurrentPeriodEndUtc)
            .IsRequired();

        builder.Property(s => s.GracePeriodEndsAtUtc);

        builder.Property(s => s.NextBillingDateUtc);

        builder.Property(s => s.GatewayCustomerId)
            .HasMaxLength(150);

        builder.Property(s => s.GatewaySubscriptionId)
            .HasMaxLength(150);

        builder.Property(s => s.StatusReason)
            .HasMaxLength(500);

        builder.Property(s => s.UpdatedAtUtc)
            .IsRequired();
    }
}
