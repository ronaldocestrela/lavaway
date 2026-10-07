using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class TenantSaasSubscriptionRepository(BillingDbContext dbContext) : ITenantSaasSubscriptionRepository
{
    public async Task<TenantSaasSubscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.TenantSaasSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
    }

    public async Task<TenantSaasSubscription?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId, CancellationToken ct = default)
    {
        return await dbContext.TenantSaasSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.GatewaySubscriptionId == gatewaySubscriptionId, ct);
    }

    public async Task<IReadOnlyList<TenantSaasSubscription>> ListAllAsync(CancellationToken ct = default)
    {
        return await dbContext.TenantSaasSubscriptions
            .IgnoreQueryFilters()
            .OrderByDescending(s => s.UpdatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TenantSaasSubscription subscription, CancellationToken ct = default)
    {
        await dbContext.TenantSaasSubscriptions.AddAsync(subscription, ct);
    }

    public void Update(TenantSaasSubscription subscription)
    {
        dbContext.TenantSaasSubscriptions.Update(subscription);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
