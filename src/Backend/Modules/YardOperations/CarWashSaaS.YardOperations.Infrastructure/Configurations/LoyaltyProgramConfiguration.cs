using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class LoyaltyProgramConfiguration : IEntityTypeConfiguration<LoyaltyProgram>
{
    public void Configure(EntityTypeBuilder<LoyaltyProgram> builder)
    {
        builder.ToTable("LoyaltyPrograms", "yard");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.IsEnabled).IsRequired();
        builder.Property(p => p.TargetStamps).IsRequired();
        builder.Property(p => p.RewardTitle).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ProximityThreshold).IsRequired();
        builder.Property(p => p.AllServicesEligible).IsRequired();
        builder.Property(p => p.EligibleCategoryFilter).HasMaxLength(500);
        builder.Property(p => p.UpdatedAtUtc).IsRequired();

        builder.HasIndex(p => p.TenantId).IsUnique();
    }
}
