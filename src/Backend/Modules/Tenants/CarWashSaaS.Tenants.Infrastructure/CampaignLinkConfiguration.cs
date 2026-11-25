using CarWashSaaS.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.Tenants.Infrastructure;

public sealed class CampaignLinkConfiguration : IEntityTypeConfiguration<CampaignLink>
{
    public void Configure(EntityTypeBuilder<CampaignLink> builder)
    {
        builder.ToTable("CampaignLinks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(c => c.Slug)
            .IsUnique();

        builder.Property(c => c.DestinationPath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.UtmSource)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.UtmMedium)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.UtmCampaign)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.UtmTerm)
            .HasMaxLength(150);

        builder.Property(c => c.UtmContent)
            .HasMaxLength(150);

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.Property(c => c.ClickCount)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        builder.Property(c => c.CreatedByUserId)
            .IsRequired();
    }
}
