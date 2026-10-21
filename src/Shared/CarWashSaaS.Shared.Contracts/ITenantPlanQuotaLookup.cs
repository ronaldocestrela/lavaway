namespace CarWashSaaS.Shared.Contracts;

public interface ITenantPlanQuotaLookup
{
    Task<Result<TenantQuotaStatusDto>> CheckWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result> ConsumeWorkOrderQuotaAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<TenantQuotaStatusDto>> CheckWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result> ConsumeWhatsAppQuotaAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<TenantSubscriptionOverviewDto>> GetSubscriptionOverviewAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<bool>> HasFeatureAsync(Guid tenantId, SaasFeature feature, CancellationToken ct = default);
    Task<Result> ValidateTeamMemberLimitAsync(Guid tenantId, int currentTeamMemberCount, CancellationToken ct = default);
    Task<Result> ValidateBoxesLimitAsync(Guid tenantId, int requestedBoxes, CancellationToken ct = default);
}
