using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public interface ISaasPlanRepository
{
    Task<IReadOnlyList<SaasPlan>> ListActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SaasPlan>> ListAllAsync(CancellationToken ct = default);
    Task<SaasPlan?> GetByTierAsync(SaasPlanTier tier, CancellationToken ct = default);
    Task AddAsync(SaasPlan plan, CancellationToken ct = default);
    Task UpdateAsync(SaasPlan plan, CancellationToken ct = default);
    Task EnsureSeedDataAsync(CancellationToken ct = default);
}
