using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default);
    Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, Guid? excludeVehicleId, CancellationToken ct = default) =>
        IsPlateRegisteredAsync(tenantId, normalizedPlate, ct);
    Task AddAsync(Vehicle vehicle, CancellationToken ct = default);
}
