using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class TenantSaasSubscriptionRepository(
    BillingDbContext dbContext,
    ICurrentTenantAccessor? currentTenantAccessor = null) : ITenantSaasSubscriptionRepository
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
        EnsureTenantContext(subscription.TenantId);
        await dbContext.TenantSaasSubscriptions.AddAsync(subscription, ct);
    }

    public void Update(TenantSaasSubscription subscription)
    {
        EnsureTenantContext(subscription.TenantId);
        dbContext.TenantSaasSubscriptions.Update(subscription);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await dbContext.SaveChangesAsync(ct);
    }

    private void EnsureTenantContext(Guid tenantId)
    {
        if (currentTenantAccessor?.TenantId is null && currentTenantAccessor is CurrentTenantAccessor mutableAccessor)
        {
            mutableAccessor.SetTenant(tenantId);
        }
    }
}
