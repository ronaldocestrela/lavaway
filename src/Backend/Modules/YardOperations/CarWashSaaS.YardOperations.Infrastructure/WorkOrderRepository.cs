using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class WorkOrderRepository(YardOperationsDbContext dbContext) : IWorkOrderRepository
{
    public Task<WorkOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        dbContext.WorkOrders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .FirstOrDefaultAsync(order => order.TenantId == tenantId && order.Id == id, ct);

    public async Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
        await dbContext.WorkOrders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Where(order => order.TenantId == tenantId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-2);
        return await dbContext.WorkOrders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Where(order => order.TenantId == tenantId && (order.Status != WorkOrderStatus.ReadyForPickup || order.CreatedAtUtc >= cutoff))
            .OrderBy(order => order.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(WorkOrder workOrder, CancellationToken ct = default) =>
        await dbContext.WorkOrders.AddAsync(workOrder, ct);
}
