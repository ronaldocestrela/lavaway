using CarWashSaaS.Billing.Domain;

namespace CarWashSaaS.Billing.Application;

public interface ISubscriptionPlanRepository
{
    Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default);
    Task<SubscriptionPlan?> GetByIdAsync(Guid tenantId, Guid planId, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPlan>> ListAllAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPlan>> ListActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
