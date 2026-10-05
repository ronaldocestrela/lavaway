using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class CustomerSubscriptionRepository(BillingDbContext dbContext) : ICustomerSubscriptionRepository
{
    public async Task AddAsync(CustomerSubscription subscription, CancellationToken ct = default)
    {
        await dbContext.CustomerSubscriptions.AddAsync(subscription, ct);
    }

    public async Task<CustomerSubscription?> GetByIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default)
    {
        return await dbContext.CustomerSubscriptions
            .Include(s => s.AuthorizedPlates)
            .Include(s => s.Usages)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == subscriptionId, ct);
    }

    public async Task<CustomerSubscription?> GetActiveByPlateAsync(
        Guid tenantId,
        string plate,
        DateTimeOffset nowUtc,
        CancellationToken ct = default)
    {
        var normalized = SubscriptionVehiclePlate.NormalizePlate(plate);

        return await dbContext.CustomerSubscriptions
            .Include(s => s.AuthorizedPlates)
            .Include(s => s.Usages)
            .Where(s => s.TenantId == tenantId &&
                        s.Status == SubscriptionStatusConstants.Active &&
                        s.CurrentPeriodStartUtc <= nowUtc &&
                        s.CurrentPeriodEndUtc >= nowUtc &&
                        s.AuthorizedPlates.Any(p => p.Plate == normalized))
            .OrderByDescending(s => s.CurrentPeriodEndUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerSubscription>> ListByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        return await dbContext.CustomerSubscriptions
            .Include(s => s.AuthorizedPlates)
            .Include(s => s.Usages)
            .Where(s => s.TenantId == tenantId && s.CustomerId == customerId)
            .OrderByDescending(s => s.CreatedUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerSubscription>> ListAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.CustomerSubscriptions
            .Include(s => s.AuthorizedPlates)
            .Include(s => s.Usages)
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SubscriptionUsage>> ListUsagesBySubscriptionIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default)
    {
        return await dbContext.SubscriptionUsages
            .Where(u => u.TenantId == tenantId && u.CustomerSubscriptionId == subscriptionId)
            .OrderByDescending(u => u.ConsumedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.CustomerSubscriptions
            .CountAsync(s => s.TenantId == tenantId && s.Status == SubscriptionStatusConstants.Active, ct);
    }

    public async Task<decimal> GetEstimatedMonthlyRevenueAsync(Guid tenantId, CancellationToken ct = default)
    {
        var activePlanIds = await dbContext.CustomerSubscriptions
            .Where(s => s.TenantId == tenantId && s.Status == SubscriptionStatusConstants.Active)
            .Select(s => s.PlanId)
            .ToListAsync(ct);

        if (activePlanIds.Count == 0)
        {
            return 0m;
        }

        var plans = await dbContext.SubscriptionPlans
            .Where(p => p.TenantId == tenantId && activePlanIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.MonthlyPrice, ct);

        decimal totalRevenue = 0m;
        foreach (var planId in activePlanIds)
        {
            if (plans.TryGetValue(planId, out var price))
            {
                totalRevenue += price;
            }
        }

        return totalRevenue;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}
