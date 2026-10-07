namespace CarWashSaaS.Shared.Contracts;

public interface ITenantPlanQuotaLookup
{
    Task<Result<TenantQuotaStatusDto>> CheckWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result> ConsumeWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<TenantQuotaStatusDto>> CheckWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result> ConsumeWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<TenantSubscriptionOverviewDto>> GetSubscriptionOverviewAsync(Guid tenantId, CancellationToken ct = default);
}
