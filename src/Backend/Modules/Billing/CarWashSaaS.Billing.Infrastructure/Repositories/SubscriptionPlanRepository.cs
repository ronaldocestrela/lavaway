using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class SubscriptionPlanRepository(BillingDbContext dbContext) : ISubscriptionPlanRepository
{
    public async Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        await dbContext.SubscriptionPlans.AddAsync(plan, ct);
    }

    public async Task<SubscriptionPlan?> GetByIdAsync(Guid tenantId, Guid planId, CancellationToken ct = default)
    {
        return await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == planId, ct);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> ListAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.SubscriptionPlans
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.MonthlyPrice)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> ListActiveAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.SubscriptionPlans
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.MonthlyPrice)
            .ToListAsync(ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}
