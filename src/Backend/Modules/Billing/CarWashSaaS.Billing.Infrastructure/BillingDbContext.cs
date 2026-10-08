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
    public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();
    public DbSet<DailyCashClosing> DailyCashClosings => Set<DailyCashClosing>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<CustomerSubscription> CustomerSubscriptions => Set<CustomerSubscription>();
    public DbSet<SubscriptionVehiclePlate> SubscriptionVehiclePlates => Set<SubscriptionVehiclePlate>();
    public DbSet<SubscriptionUsage> SubscriptionUsages => Set<SubscriptionUsage>();
    public DbSet<SaasPlan> SaasPlans => Set<SaasPlan>();
    public DbSet<TenantSaasSubscription> TenantSaasSubscriptions => Set<TenantSaasSubscription>();
    public DbSet<TenantQuotaUsage> TenantQuotaUsages => Set<TenantQuotaUsage>();
    public DbSet<SaasInvoice> SaasInvoices => Set<SaasInvoice>();
    public DbSet<ProcessedSaasWebhookEvent> ProcessedSaasWebhookEvents => Set<ProcessedSaasWebhookEvent>();
    public DbSet<TenantPaymentGatewayConfig> TenantPaymentGatewayConfigs => Set<TenantPaymentGatewayConfig>();

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
