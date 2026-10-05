using CarWashSaaS.Billing.Domain;

namespace CarWashSaaS.Billing.Application;

public interface ICustomerSubscriptionRepository
{
    Task AddAsync(CustomerSubscription subscription, CancellationToken ct = default);
    Task<CustomerSubscription?> GetByIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default);
    Task<CustomerSubscription?> GetActiveByPlateAsync(Guid tenantId, string plate, DateTimeOffset nowUtc, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerSubscription>> ListByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerSubscription>> ListAllAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionUsage>> ListUsagesBySubscriptionIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default);
    Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task<decimal> GetEstimatedMonthlyRevenueAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
