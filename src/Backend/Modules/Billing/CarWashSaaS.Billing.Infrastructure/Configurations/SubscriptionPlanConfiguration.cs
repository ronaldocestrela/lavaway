using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans", "billing");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.MonthlyPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.CreditsPerCycle)
            .IsRequired();

        builder.Property(p => p.AllowedPlatesLimit)
            .IsRequired();

        builder.Property(p => p.BillingIntervalDays)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.Property(p => p.CreatedUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedUtc);

        builder.HasIndex(p => new { p.TenantId, p.IsActive });
    }
}
