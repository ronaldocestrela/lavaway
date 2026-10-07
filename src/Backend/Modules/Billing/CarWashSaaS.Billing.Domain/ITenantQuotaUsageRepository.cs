namespace CarWashSaaS.Billing.Domain;

public interface ITenantQuotaUsageRepository
{
    Task<TenantQuotaUsage?> GetCurrentCycleByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantQuotaUsage quotaUsage, CancellationToken ct = default);
    void Update(TenantQuotaUsage quotaUsage);
    Task SaveChangesAsync(CancellationToken ct = default);
}
