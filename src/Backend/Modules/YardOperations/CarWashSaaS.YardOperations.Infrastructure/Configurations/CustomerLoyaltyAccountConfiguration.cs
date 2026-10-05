using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class CustomerLoyaltyAccountConfiguration : IEntityTypeConfiguration<CustomerLoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<CustomerLoyaltyAccount> builder)
    {
        builder.ToTable("CustomerLoyaltyAccounts", "yard");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.CustomerId).IsRequired();
        builder.Property(a => a.Balance).IsRequired();
        builder.Property(a => a.TotalEarned).IsRequired();
        builder.Property(a => a.TotalRedeemed).IsRequired();
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.LastAccrualAtUtc);
        builder.Property(a => a.LastRedemptionAtUtc);

        builder.HasIndex(a => new { a.TenantId, a.CustomerId }).IsUnique();

        builder.HasMany(a => a.Transactions)
            .WithOne()
            .HasForeignKey(t => t.CustomerLoyaltyAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Transactions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
