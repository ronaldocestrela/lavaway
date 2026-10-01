using CarWashSaaS.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Tenants.Infrastructure;

public sealed class TenantsDbContext(DbContextOptions<TenantsDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tenant = modelBuilder.Entity<Tenant>();
        tenant.ToTable("Tenants", "tenants");
        tenant.HasKey(value => value.Id);
        tenant.Property(value => value.Id).ValueGeneratedNever();
        tenant.Property(value => value.Name).HasMaxLength(200).IsRequired();
        tenant.Property(value => value.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
    }
}