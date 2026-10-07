namespace CarWashSaaS.Shared.Contracts;

public interface IGlobalTenantLookup
{
    Task<Result<bool>> ExistsAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<GlobalTenantSummaryDto>> GetSummaryAsync(Guid tenantId, CancellationToken ct = default);
}
