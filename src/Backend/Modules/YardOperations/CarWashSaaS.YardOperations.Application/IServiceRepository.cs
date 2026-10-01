using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface IServiceRepository
{
    Task<IReadOnlyCollection<Service>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<Service?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Service service, CancellationToken ct = default);
    Task UpdateAsync(Service service, CancellationToken ct = default);
}
