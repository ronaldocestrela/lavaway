using CarWashSaaS.Billing.Domain;

namespace CarWashSaaS.Billing.Application;

public interface IDailyCashClosingRepository
{
    Task AddAsync(DailyCashClosing closing, CancellationToken ct = default);
    Task UpdateAsync(DailyCashClosing closing, CancellationToken ct = default);
    Task<DailyCashClosing?> GetByDateAsync(Guid tenantId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<DailyCashClosing>> ListRecentAsync(Guid tenantId, int count = 30, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
