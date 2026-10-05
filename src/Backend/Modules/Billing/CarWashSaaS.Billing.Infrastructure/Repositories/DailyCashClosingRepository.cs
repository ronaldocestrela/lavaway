using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class DailyCashClosingRepository(BillingDbContext dbContext) : IDailyCashClosingRepository
{
    public async Task AddAsync(DailyCashClosing closing, CancellationToken ct = default)
    {
        await dbContext.DailyCashClosings.AddAsync(closing, ct);
    }

    public Task UpdateAsync(DailyCashClosing closing, CancellationToken ct = default)
    {
        dbContext.DailyCashClosings.Update(closing);
        return Task.CompletedTask;
    }

    public async Task<DailyCashClosing?> GetByDateAsync(Guid tenantId, DateOnly date, CancellationToken ct = default)
    {
        return await dbContext.DailyCashClosings
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ClosingDate == date, ct);
    }

    public async Task<IReadOnlyList<DailyCashClosing>> ListRecentAsync(Guid tenantId, int count = 30, CancellationToken ct = default)
    {
        return await dbContext.DailyCashClosings
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.ClosingDate)
            .Take(count)
            .ToListAsync(ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}
