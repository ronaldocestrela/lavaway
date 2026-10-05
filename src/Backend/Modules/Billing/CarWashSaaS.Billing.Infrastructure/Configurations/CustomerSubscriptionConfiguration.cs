using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class CustomerSubscriptionConfiguration : IEntityTypeConfiguration<CustomerSubscription>
{
    public void Configure(EntityTypeBuilder<CustomerSubscription> builder)
    {
        builder.ToTable("CustomerSubscriptions", "billing");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.CustomerId)
            .IsRequired();

        builder.Property(s => s.CustomerName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(s => s.CustomerPhone)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.PlanId)
            .IsRequired();

        builder.Property(s => s.PlanName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.CurrentPeriodStartUtc)
            .IsRequired();

        builder.Property(s => s.CurrentPeriodEndUtc)
            .IsRequired();

        builder.Property(s => s.TotalCreditsInCycle)
            .IsRequired();

        builder.Property(s => s.UsedCreditsInCycle)
            .IsRequired();

        builder.Property(s => s.GatewaySubscriptionId)
            .HasMaxLength(100);

        builder.Property(s => s.CardLastFourDigits)
            .HasMaxLength(10);

        builder.Property(s => s.CardBrand)
            .HasMaxLength(30);

        builder.Property(s => s.CreatedUtc)
            .IsRequired();

        builder.Property(s => s.CanceledAtUtc);

        builder.Property(s => s.CancelReason)
            .HasMaxLength(250);

        builder.HasIndex(s => new { s.TenantId, s.CustomerId });
        builder.HasIndex(s => new { s.TenantId, s.Status });
        builder.HasIndex(s => new { s.TenantId, s.GatewaySubscriptionId });

        builder.HasMany(s => s.AuthorizedPlates)
            .WithOne()
            .HasForeignKey(p => p.CustomerSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.AuthorizedPlates)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(s => s.Usages)
            .WithOne()
            .HasForeignKey(u => u.CustomerSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Usages)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
