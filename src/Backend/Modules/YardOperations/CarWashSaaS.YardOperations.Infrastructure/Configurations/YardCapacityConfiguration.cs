using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class YardCapacityConfiguration : IEntityTypeConfiguration<YardCapacity>
{
    public void Configure(EntityTypeBuilder<YardCapacity> builder)
    {
        builder.ToTable("YardCapacities", "yard");
        builder.HasKey(capacity => capacity.Id);
        builder.Property(capacity => capacity.Id).ValueGeneratedNever();
        builder.Property(capacity => capacity.TenantId).IsRequired();
        builder.Property(capacity => capacity.TotalBoxes).IsRequired();
        builder.Property(capacity => capacity.Description).HasMaxLength(200).IsRequired();
        builder.HasAlternateKey(capacity => new { capacity.TenantId, capacity.Id });
    }
}
