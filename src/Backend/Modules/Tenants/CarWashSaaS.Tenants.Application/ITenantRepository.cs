using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.Tenants.Application;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<GlobalTenantSummaryDto>> SearchGlobalTenantsAsync(GetGlobalTenantsRequest request, CancellationToken ct = default);
    Task<GlobalTenantSummaryDto?> GetSummaryByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateAsync(Tenant tenant, CancellationToken ct = default);
}
