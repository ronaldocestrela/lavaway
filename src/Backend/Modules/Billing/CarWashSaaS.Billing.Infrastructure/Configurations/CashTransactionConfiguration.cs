using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class CashTransactionConfiguration : IEntityTypeConfiguration<CashTransaction>
{
    public void Configure(EntityTypeBuilder<CashTransaction> builder)
    {
        builder.ToTable("CashTransactions", "billing");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId)
            .IsRequired();

        builder.Property(t => t.WorkOrderId);

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.PaymentMethod)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(t => t.OccurredAtUtc)
            .IsRequired();

        builder.Property(t => t.RegisteredByUserId);

        builder.Property(t => t.RegisteredByUserName)
            .HasMaxLength(150);

        builder.Property(t => t.ExternalReference)
            .HasMaxLength(150);

        builder.HasIndex(t => new { t.TenantId, t.OccurredAtUtc });
        builder.HasIndex(t => new { t.TenantId, t.WorkOrderId });
        builder.HasIndex(t => new { t.TenantId, t.PaymentMethod });
    }
}
