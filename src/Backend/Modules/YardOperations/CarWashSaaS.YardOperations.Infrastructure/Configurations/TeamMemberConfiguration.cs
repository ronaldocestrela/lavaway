using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers", "yard");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).ValueGeneratedNever();
        builder.Property(member => member.TenantId).IsRequired();
        builder.Property(member => member.FullName).HasMaxLength(200).IsRequired();
        builder.Property(member => member.Role).HasMaxLength(80).IsRequired();
        builder.Property(member => member.Email).HasMaxLength(200);
        builder.Property(member => member.IsActive).IsRequired();
        builder.HasIndex(member => new { member.TenantId, member.Email })
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL AND [Email] <> ''");
    }
}
