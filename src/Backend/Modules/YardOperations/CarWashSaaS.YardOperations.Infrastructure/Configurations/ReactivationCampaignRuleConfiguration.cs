using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class ReactivationCampaignRuleConfiguration : IEntityTypeConfiguration<ReactivationCampaignRule>
{
    public void Configure(EntityTypeBuilder<ReactivationCampaignRule> builder)
    {
        builder.ToTable("ReactivationCampaignRules", "yard");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.TenantId).IsRequired();
        builder.Property(rule => rule.DaysInactive).IsRequired();
        builder.Property(rule => rule.Title).HasMaxLength(200).IsRequired();
        builder.Property(rule => rule.MessageTemplate).HasMaxLength(2000).IsRequired();
        builder.Property(rule => rule.IsEnabled).IsRequired();
        builder.Property(rule => rule.PromotionalOffer).HasMaxLength(200);
        builder.Property(rule => rule.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(rule => rule.UpdatedAtUtc).HasColumnType("datetimeoffset").IsRequired();

        builder.HasAlternateKey(rule => new { rule.TenantId, rule.Id });
        builder.HasIndex(rule => new { rule.TenantId, rule.DaysInactive }).IsUnique();
    }
}
