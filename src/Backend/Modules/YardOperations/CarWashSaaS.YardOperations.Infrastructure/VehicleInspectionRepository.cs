using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class VehicleInspectionRepository(YardOperationsDbContext dbContext) : IVehicleInspectionRepository
{
    public Task<VehicleInspection?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        dbContext.VehicleInspections
            .Include(i => i.Damages)
            .Include(i => i.ChecklistItems)
            .Include(i => i.Photos)
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == id, ct);

    public Task<VehicleInspection?> GetByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
        dbContext.VehicleInspections
            .Include(i => i.Damages)
            .Include(i => i.ChecklistItems)
            .Include(i => i.Photos)
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.WorkOrderId == workOrderId, ct);

    public async Task AddAsync(VehicleInspection inspection, CancellationToken ct = default) =>
        await dbContext.VehicleInspections.AddAsync(inspection, ct);

    public void Update(VehicleInspection inspection) =>
        dbContext.VehicleInspections.Update(inspection);

    public async Task<IReadOnlyList<VehicleInspection>> GetInspectionsWithPhotosEligibleForPurgeAsync(
        Guid tenantId,
        DateTimeOffset cutoffUtc,
        int limit = 100,
        CancellationToken ct = default)
    {
        var eligibleWorkOrderIds = dbContext.WorkOrders
            .Where(w => w.TenantId == tenantId && w.PickedUpAtUtc != null && w.PickedUpAtUtc <= cutoffUtc)
            .Select(w => w.Id);

        return await dbContext.VehicleInspections
            .Include(i => i.Photos)
            .Where(i => i.TenantId == tenantId
                && eligibleWorkOrderIds.Contains(i.WorkOrderId)
                && i.Photos.Any(p => !p.IsPurged))
            .Take(limit)
            .ToListAsync(ct);
    }
}
