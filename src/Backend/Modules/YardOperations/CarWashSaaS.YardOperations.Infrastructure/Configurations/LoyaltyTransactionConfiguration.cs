using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.ToTable("LoyaltyTransactions", "yard");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.CustomerLoyaltyAccountId).IsRequired();
        builder.Property(t => t.Type).HasConversion<int>().IsRequired();
        builder.Property(t => t.Amount).IsRequired();
        builder.Property(t => t.BalanceAfter).IsRequired();
        builder.Property(t => t.WorkOrderId);
        builder.Property(t => t.WorkOrderNumber).HasMaxLength(60);
        builder.Property(t => t.Description).HasMaxLength(500).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.CustomerLoyaltyAccountId });
        builder.HasIndex(t => new { t.TenantId, t.CustomerLoyaltyAccountId, t.WorkOrderId })
            .IsUnique()
            .HasFilter("[WorkOrderId] IS NOT NULL AND [Type] = 1"); // 1 = Accrual
    }
}
