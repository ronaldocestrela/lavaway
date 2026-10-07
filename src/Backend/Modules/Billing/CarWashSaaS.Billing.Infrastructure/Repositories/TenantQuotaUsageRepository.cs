using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class TenantQuotaUsageRepository(BillingDbContext dbContext) : ITenantQuotaUsageRepository
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
        await dbContext.TenantQuotaUsages.AddAsync(quotaUsage, ct);
    }

    public void Update(TenantQuotaUsage quotaUsage)
    {
        dbContext.TenantQuotaUsages.Update(quotaUsage);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
