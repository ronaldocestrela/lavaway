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
            .Include(order => order.PostServicePhotos)
            .FirstOrDefaultAsync(order => order.TenantId == tenantId && order.Id == id, ct);

    public async Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
        await dbContext.WorkOrders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.PostServicePhotos)
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
            .Include(order => order.PostServicePhotos)
            .Where(order => order.TenantId == tenantId &&
                            !order.PickedUpAtUtc.HasValue &&
                            (order.Status != WorkOrderStatus.ReadyForPickup || order.CreatedAtUtc >= cutoff))
            .OrderBy(order => order.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<WorkOrder>> GetWorkOrdersPendingSurveyAsync(Guid tenantId, DateTimeOffset cutoff, CancellationToken ct = default) =>
        await dbContext.WorkOrders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.PostServicePhotos)
            .Where(order => order.TenantId == tenantId &&
                            order.PickedUpAtUtc.HasValue &&
                            order.PickedUpAtUtc.Value <= cutoff &&
                            !order.SurveySentAtUtc.HasValue)
            .OrderBy(order => order.PickedUpAtUtc)
            .ToListAsync(ct);

    public async Task<WorkOrder?> GetLatestCompletedOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default)
    {
        var normalized = Customer.NormalizePhone(customerPhone);
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && (c.NormalizedPhone == normalized || c.Phone == customerPhone), ct);

        if (customer is null) return null;

        return await dbContext.WorkOrders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.PostServicePhotos)
            .Where(order => order.TenantId == tenantId && order.CustomerId == customer.Id && order.PickedUpAtUtc.HasValue)
            .OrderByDescending(order => order.PickedUpAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default) =>
        await dbContext.WorkOrders
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.PostServicePhotos)
            .Where(order => order.TenantId == tenantId && order.SurveySentAtUtc.HasValue)
            .OrderByDescending(order => order.SurveySentAtUtc)
            .Take(limit)
            .ToListAsync(ct);

    public async Task AddAsync(WorkOrder workOrder, CancellationToken ct = default) =>
        await dbContext.WorkOrders.AddAsync(workOrder, ct);

    public void Update(WorkOrder workOrder) =>
        dbContext.WorkOrders.Update(workOrder);
}
