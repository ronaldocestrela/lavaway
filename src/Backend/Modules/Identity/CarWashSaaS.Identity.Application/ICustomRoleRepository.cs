using CarWashSaaS.Identity.Domain;

namespace CarWashSaaS.Identity.Application;

public interface ICustomRoleRepository
{
    Task<CustomRole?> GetByIdAsync(Guid tenantId, Guid roleId, CancellationToken ct = default);
    Task<CustomRole?> GetByNameAsync(Guid tenantId, string name, CancellationToken ct = default);
    Task<IReadOnlyCollection<CustomRole>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> CountUsersPerRoleAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> HasAssignedUsersAsync(Guid tenantId, Guid roleId, CancellationToken ct = default);
    Task AddAsync(CustomRole role, CancellationToken ct = default);
    void Update(CustomRole role);
    void Remove(CustomRole role);
    Task SaveChangesAsync(CancellationToken ct = default);
}
