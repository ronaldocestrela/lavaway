using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class WorkOrderRepository(YardOperationsDbContext dbContext) : IWorkOrderRepository
{
    public Task<WorkOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        dbContext.WorkOrders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.TenantId == tenantId && order.Id == id, ct);

    public async Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
        await dbContext.WorkOrders
            .Include(order => order.Items)
            .Where(order => order.TenantId == tenantId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

    public async Task AddAsync(WorkOrder workOrder, CancellationToken ct = default) =>
        await dbContext.WorkOrders.AddAsync(workOrder, ct);
}
