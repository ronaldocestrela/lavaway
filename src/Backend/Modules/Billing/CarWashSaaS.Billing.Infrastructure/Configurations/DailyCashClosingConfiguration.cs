using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Billing.Infrastructure.Configurations;

public sealed class DailyCashClosingConfiguration : IEntityTypeConfiguration<DailyCashClosing>
{
    public void Configure(EntityTypeBuilder<DailyCashClosing> builder)
    {
        builder.ToTable("DailyCashClosings", "billing");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.ClosingDate)
            .IsRequired();

        builder.Property(c => c.ClosedAtUtc)
            .IsRequired();

        builder.Property(c => c.ClosedByUserId)
            .IsRequired();

        builder.Property(c => c.ClosedByUserName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.TotalIncome)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.TotalPix)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.TotalCash)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.TotalCreditCard)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.TotalDebitCard)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.TotalSupplies)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.TotalBleeds)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.ExpectedCashInDrawer)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.ActualCashInDrawer)
            .HasPrecision(18, 2);

        builder.Property(c => c.CashDifference)
            .HasPrecision(18, 2);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.Notes)
            .HasMaxLength(500);

        builder.HasIndex(c => new { c.TenantId, c.ClosingDate })
            .IsUnique();
    }
}
