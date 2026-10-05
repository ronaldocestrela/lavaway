using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class ReactivationCampaignLogConfiguration : IEntityTypeConfiguration<ReactivationCampaignLog>
{
    public void Configure(EntityTypeBuilder<ReactivationCampaignLog> builder)
    {
        builder.ToTable("ReactivationCampaignLogs", "yard");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.Property(log => log.TenantId).IsRequired();
        builder.Property(log => log.CampaignRuleId).IsRequired();
        builder.Property(log => log.CustomerId).IsRequired();
        builder.Property(log => log.CustomerPhone).HasMaxLength(32).IsRequired();
        builder.Property(log => log.DaysInactive).IsRequired();
        builder.Property(log => log.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(log => log.SentAtUtc).HasColumnType("datetimeoffset").IsRequired();

        builder.HasAlternateKey(log => new { log.TenantId, log.Id });
        builder.HasIndex(log => new { log.TenantId, log.CustomerId, log.SentAtUtc });
        builder.HasIndex(log => new { log.TenantId, log.IdempotencyKey }).IsUnique();
    }
}
