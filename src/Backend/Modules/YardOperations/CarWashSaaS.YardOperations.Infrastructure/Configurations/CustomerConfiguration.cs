using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarWashSaaS.YardOperations.Infrastructure.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", "yard");
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Id).ValueGeneratedNever();
        builder.Property(customer => customer.TenantId).IsRequired();
        builder.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
        builder.Property(customer => customer.Phone).HasMaxLength(32).IsRequired();
        builder.Property(customer => customer.NormalizedPhone).HasMaxLength(32).IsRequired();
        builder.HasAlternateKey(customer => new { customer.TenantId, customer.Id });
        builder.HasIndex(customer => new { customer.TenantId, customer.NormalizedPhone });
    }
}