using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface IYardCapacityRepository
{
    Task<YardCapacity?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(YardCapacity yardCapacity, CancellationToken ct = default);
    Task UpdateAsync(YardCapacity yardCapacity, CancellationToken ct = default);
}
