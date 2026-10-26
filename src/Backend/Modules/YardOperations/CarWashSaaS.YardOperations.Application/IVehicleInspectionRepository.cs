using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface IVehicleInspectionRepository
{
    Task<VehicleInspection?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<VehicleInspection?> GetByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default);
    Task AddAsync(VehicleInspection inspection, CancellationToken ct = default);
    void Update(VehicleInspection inspection);

    Task<IReadOnlyList<VehicleInspection>> GetInspectionsWithPhotosEligibleForPurgeAsync(
        Guid tenantId,
        DateTimeOffset cutoffUtc,
        int limit = 100,
        CancellationToken ct = default);
}
