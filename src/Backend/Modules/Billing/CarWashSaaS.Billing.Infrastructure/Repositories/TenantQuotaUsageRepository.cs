using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class TenantQuotaUsageRepository(
    BillingDbContext dbContext,
    ICurrentTenantAccessor? currentTenantAccessor = null) : ITenantQuotaUsageRepository
{
    public async Task<TenantQuotaUsage?> GetCurrentCycleByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.TenantQuotaUsages
            .IgnoreQueryFilters()
            .OrderByDescending(q => q.CycleEndUtc)
            .FirstOrDefaultAsync(q => q.TenantId == tenantId, ct);
    }

    public async Task AddAsync(TenantQuotaUsage quotaUsage, CancellationToken ct = default)
    {
        EnsureTenantContext(quotaUsage.TenantId);
        await dbContext.TenantQuotaUsages.AddAsync(quotaUsage, ct);
    }

    public void Update(TenantQuotaUsage quotaUsage)
    {
        EnsureTenantContext(quotaUsage.TenantId);
        dbContext.TenantQuotaUsages.Update(quotaUsage);
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
