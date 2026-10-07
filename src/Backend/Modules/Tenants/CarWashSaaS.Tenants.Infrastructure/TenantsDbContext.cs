using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Tenants.Infrastructure;

public sealed class TenantsDbContext(
    DbContextOptions<TenantsDbContext> options,
    ICurrentTenantAccessor currentTenantAccessor) : DbContext(options)
{
    public Guid? TenantId => currentTenantAccessor.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<StoreProfile> StoreProfiles => Set<StoreProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tenant = modelBuilder.Entity<Tenant>();
        tenant.ToTable("Tenants", "tenants");
        tenant.HasKey(value => value.Id);
        tenant.Property(value => value.Id).ValueGeneratedNever();
        tenant.Property(value => value.Name).HasMaxLength(200).IsRequired();
        tenant.Property(value => value.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        tenant.Property(value => value.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        tenant.Property(value => value.StatusChangedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        tenant.Property(value => value.TrialEndsAtUtc).HasColumnType("datetimeoffset");
        tenant.Property(value => value.StatusReason).HasMaxLength(500);
        tenant.HasIndex(value => value.Status);

        var storeProfile = modelBuilder.Entity<StoreProfile>();
        storeProfile.ToTable("StoreProfiles", "tenants");
        storeProfile.HasKey(value => value.Id);
        storeProfile.Property(value => value.Id).ValueGeneratedNever();
        storeProfile.Property(value => value.TenantId).IsRequired();
        storeProfile.Property(value => value.LegalName).HasMaxLength(200).IsRequired();
        storeProfile.Property(value => value.TradeName).HasMaxLength(200).IsRequired();
        storeProfile.Property(value => value.Cnpj).HasMaxLength(14).IsRequired();
        storeProfile.Property(value => value.Phone).HasMaxLength(32).IsRequired();
        storeProfile.Property(value => value.Street).HasMaxLength(200).IsRequired();
        storeProfile.Property(value => value.City).HasMaxLength(150).IsRequired();
        storeProfile.Property(value => value.State).HasMaxLength(2).IsRequired();
        storeProfile.Property(value => value.PostalCode).HasMaxLength(20).IsRequired();
        storeProfile.Property(value => value.LogoUrl).HasMaxLength(500);
        storeProfile.Property(value => value.BrandPrimaryColor).HasMaxLength(7);
        storeProfile.Property(value => value.BrandSecondaryColor).HasMaxLength(7);

        modelBuilder.ApplyTenantQueryFilters(this);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.ValidateTenantWrites(currentTenantAccessor);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.ValidateTenantWrites(currentTenantAccessor);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
