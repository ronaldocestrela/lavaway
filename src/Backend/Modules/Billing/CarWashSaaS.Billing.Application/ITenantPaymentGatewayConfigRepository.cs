using CarWashSaaS.Billing.Domain;

namespace CarWashSaaS.Billing.Application;

public interface ITenantPaymentGatewayConfigRepository
{
    Task<TenantPaymentGatewayConfig?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantPaymentGatewayConfig config, CancellationToken ct = default);
    void Update(TenantPaymentGatewayConfig config);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
