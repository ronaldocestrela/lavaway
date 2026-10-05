using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class CashTransactionRepository(BillingDbContext dbContext) : ICashTransactionRepository
{
    public async Task AddAsync(CashTransaction transaction, CancellationToken ct = default)
    {
        await dbContext.CashTransactions.AddAsync(transaction, ct);
    }

    public async Task<IReadOnlyList<CashTransaction>> ListByDateAsync(Guid tenantId, DateOnly date, CancellationToken ct = default)
    {
        var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await dbContext.CashTransactions
            .Where(t => t.TenantId == tenantId && t.OccurredAtUtc >= startUtc && t.OccurredAtUtc <= endUtc)
            .OrderByDescending(t => t.OccurredAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CashTransaction>> ListByPeriodAsync(Guid tenantId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var startUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await dbContext.CashTransactions
            .Where(t => t.TenantId == tenantId && t.OccurredAtUtc >= startUtc && t.OccurredAtUtc <= endUtc)
            .OrderByDescending(t => t.OccurredAtUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsForWorkOrderAsync(Guid tenantId, Guid workOrderId, CashTransactionType type, CancellationToken ct = default)
    {
        return await dbContext.CashTransactions
            .AnyAsync(t => t.TenantId == tenantId && t.WorkOrderId == workOrderId && t.Type == type, ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}
