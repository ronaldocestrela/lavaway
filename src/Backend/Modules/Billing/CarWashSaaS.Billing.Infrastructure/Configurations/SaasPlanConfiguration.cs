using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class SaasPlanConfiguration : IEntityTypeConfiguration<SaasPlan>
{
    public void Configure(EntityTypeBuilder<SaasPlan> builder)
    {
        builder.ToTable("SaasPlans", "billing");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Tier)
            .IsRequired();

        builder.HasIndex(p => p.Tier)
            .IsUnique();

        builder.Property(p => p.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.MonthlyPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.MaxWorkOrdersPerCycle)
            .IsRequired();

        builder.Property(p => p.MaxWhatsAppMessagesPerCycle)
            .IsRequired();

        builder.Property(p => p.HasCustomerSubscriptions)
            .IsRequired();

        builder.Property(p => p.HasLoyalty)
            .IsRequired();

        builder.Property(p => p.HasCommissions)
            .IsRequired();

        builder.Property(p => p.HasAiChatbot)
            .IsRequired();

        builder.Property(p => p.MaxTeamMembers)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();
    }
}
