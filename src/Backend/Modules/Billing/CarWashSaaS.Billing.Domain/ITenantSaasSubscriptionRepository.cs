namespace CarWashSaaS.Billing.Domain;

public interface ITenantSaasSubscriptionRepository
{
    Task<TenantSaasSubscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSaasSubscription?> GetByGatewaySubscriptionIdAsync(string gatewaySubscriptionId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantSaasSubscription>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(TenantSaasSubscription subscription, CancellationToken ct = default);
    void Update(TenantSaasSubscription subscription);
    Task SaveChangesAsync(CancellationToken ct = default);
}
