using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Domain;

namespace CarWashSaaS.Tenants.Application;

public interface IStoreProfileRepository
{
    Task<StoreProfile?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(StoreProfile profile, CancellationToken ct = default);
    Task UpdateAsync(StoreProfile profile, CancellationToken ct = default);
}
