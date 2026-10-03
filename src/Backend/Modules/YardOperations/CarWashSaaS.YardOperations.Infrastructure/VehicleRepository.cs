using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class VehicleRepository(YardOperationsDbContext dbContext) : IVehicleRepository
{
    public Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        dbContext.Vehicles.FirstOrDefaultAsync(vehicle => vehicle.TenantId == tenantId && vehicle.Id == id, ct);

    public Task<bool> IsPlateRegisteredAsync(Guid tenantId, string normalizedPlate, CancellationToken ct = default) =>
        dbContext.Vehicles.AnyAsync(vehicle => vehicle.TenantId == tenantId && vehicle.Plate == normalizedPlate, ct);

    public async Task AddAsync(Vehicle vehicle, CancellationToken ct = default) =>
        await dbContext.Vehicles.AddAsync(vehicle, ct);
}
