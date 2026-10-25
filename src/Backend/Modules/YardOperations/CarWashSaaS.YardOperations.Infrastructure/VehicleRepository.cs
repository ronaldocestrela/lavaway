using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class VehicleRepository(YardOperationsDbContext dbContext) : IVehicleRepository
{
    public Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        dbContext.Vehicles.FirstOrDefaultAsync(vehicle => vehicle.TenantId == tenantId && vehicle.Id == id, ct);

    public Task<Vehicle?> GetByPlateAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
        dbContext.Vehicles.FirstOrDefaultAsync(vehicle => vehicle.TenantId == tenantId && vehicle.Plate == normalizedPlate, ct);

    public async Task<IReadOnlyCollection<Vehicle>> GetVehiclesByCustomerIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default) =>
        await dbContext.Vehicles
            .Where(vehicle => vehicle.TenantId == tenantId && vehicle.CustomerId == customerId)
            .ToListAsync(ct);

    public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
        IsPlateRegisteredAsync(tenantId, normalizedPlate, null, ct);

    public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, Guid? excludeVehicleId, CancellationToken ct = default)
    {
        var query = dbContext.Vehicles
            .Where(vehicle => vehicle.TenantId == tenantId && vehicle.Plate == normalizedPlate && vehicle.CustomerId != null);

        if (excludeVehicleId.HasValue)
        {
            query = query.Where(vehicle => vehicle.Id != excludeVehicleId.Value);
        }

        return query.AnyAsync(ct);
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken ct = default) =>
        await dbContext.Vehicles.AddAsync(vehicle, ct);
}
