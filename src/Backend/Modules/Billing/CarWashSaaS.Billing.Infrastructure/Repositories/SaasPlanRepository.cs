using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class SaasPlanRepository(BillingDbContext dbContext) : ISaasPlanRepository
{
    public async Task<IReadOnlyList<SaasPlan>> ListActiveAsync(CancellationToken ct = default)
    {
        return await dbContext.SaasPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPrice)
            .ToListAsync(ct);
    }

    public async Task<SaasPlan?> GetByTierAsync(SaasPlanTier tier, CancellationToken ct = default)
    {
        return await dbContext.SaasPlans
            .FirstOrDefaultAsync(p => p.Tier == tier, ct);
    }

    public async Task AddAsync(SaasPlan plan, CancellationToken ct = default)
    {
        await dbContext.SaasPlans.AddAsync(plan, ct);
    }

    public async Task EnsureSeedDataAsync(CancellationToken ct = default)
    {
        var existingTiers = await dbContext.SaasPlans.Select(p => p.Tier).ToListAsync(ct);
        var standardPlans = SaasPlan.GetStandardPlans();

        foreach (var standardPlan in standardPlans)
        {
            if (!existingTiers.Contains(standardPlan.Tier))
            {
                await dbContext.SaasPlans.AddAsync(standardPlan, ct);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
