using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface ICustomerLoyaltyRepository
{
    Task<CustomerLoyaltyAccount?> GetByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
    Task<CustomerLoyaltyAccount?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> HasAccrualForWorkOrderAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerLoyaltyAccount>> ListAccountsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<LoyaltyTransaction>> ListTransactionsAsync(Guid tenantId, Guid accountId, CancellationToken ct = default);
    Task AddAccountAsync(CustomerLoyaltyAccount account, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
