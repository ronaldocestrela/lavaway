using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure;

public sealed class BillingDbContext(
    DbContextOptions<BillingDbContext> options,
    ICurrentTenantAccessor currentTenantAccessor) : DbContext(options)
{
    public Guid? TenantId => currentTenantAccessor.TenantId;

    public DbSet<PixCharge> PixCharges => Set<PixCharge>();
    public DbSet<ProcessedPaymentWebhook> ProcessedPaymentWebhooks => Set<ProcessedPaymentWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
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
