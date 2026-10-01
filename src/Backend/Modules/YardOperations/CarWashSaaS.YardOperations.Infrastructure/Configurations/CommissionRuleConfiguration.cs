using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> builder)
    {
        builder.ToTable("CommissionRules", "yard");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.TenantId).IsRequired();
        builder.Property(rule => rule.ServiceName).HasMaxLength(200).IsRequired();
        builder.Property(rule => rule.RoleName).HasMaxLength(80).IsRequired();
        builder.Property(rule => rule.Percentage).HasPrecision(5, 2).IsRequired();
        builder.HasIndex(rule => new { rule.TenantId, rule.ServiceName, rule.RoleName }).IsUnique();
    }
}
