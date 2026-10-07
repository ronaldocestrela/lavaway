using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Xunit;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class PlatformYardMetricsServiceTests
{
    private sealed class FakeWorkOrderRepository : IWorkOrderRepository
    {
        public readonly List<WorkOrder> WorkOrders = [];

        public Task<WorkOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(WorkOrders.FirstOrDefault(w => w.TenantId == tenantId && w.Id == id));

        public Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(WorkOrders.Where(w => w.TenantId == tenantId).Take(limit).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>(WorkOrders.Where(w => w.TenantId == tenantId && w.Status != WorkOrderStatus.ReadyForPickup).ToList());

        public Task<IReadOnlyCollection<WorkOrder>> GetWorkOrdersPendingSurveyAsync(Guid tenantId, DateTimeOffset cutoff, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task<WorkOrder?> GetLatestCompletedOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<WorkOrder?>(null);

        public Task<WorkOrder?> GetActiveOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default) =>
            Task.FromResult<WorkOrder?>(null);

        public Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<WorkOrder>>([]);

        public Task<IReadOnlyList<WorkOrder>> ListForPlatformMetricsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, CancellationToken ct = default)
        {
            var query = WorkOrders.AsEnumerable();
            if (tenantId.HasValue)
            {
                query = query.Where(w => w.TenantId == tenantId.Value);
            }

            return Task.FromResult<IReadOnlyList<WorkOrder>>(query.ToList());
        }

        public Task AddAsync(WorkOrder workOrder, CancellationToken ct = default)
        {
            WorkOrders.Add(workOrder);
            return Task.CompletedTask;
        }

        public void Update(WorkOrder workOrder) { }
    }

    [Fact]
    public async Task Should_Aggregate_Attended_Vehicles_Within_Date_Range()
    {
        var repo = new FakeWorkOrderRepository();
        var tenantId = Guid.NewGuid();
        var serviceItem = WorkOrderItem.Create(tenantId, Guid.NewGuid(), "Lavagem", 50m, 30).Value!;

        var wo1 = WorkOrder.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), [serviceItem]).Value!;
        wo1.ChangeStatus(WorkOrderStatus.InWashing);
        wo1.ChangeStatus(WorkOrderStatus.Finishing);
        wo1.ChangeStatus(WorkOrderStatus.QualityControl);
        wo1.ChangeStatus(WorkOrderStatus.ReadyForPickup);
        wo1.RegisterPickup(DateTimeOffset.UtcNow.AddDays(-2));
        repo.WorkOrders.Add(wo1);

        var wo2 = WorkOrder.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), [serviceItem]).Value!;
        wo2.ChangeStatus(WorkOrderStatus.InWashing);
        wo2.ChangeStatus(WorkOrderStatus.Finishing);
        wo2.ChangeStatus(WorkOrderStatus.QualityControl);
        wo2.ChangeStatus(WorkOrderStatus.ReadyForPickup);
        wo2.RegisterPickup(DateTimeOffset.UtcNow.AddDays(-1));
        repo.WorkOrders.Add(wo2);

        var woInProgress = WorkOrder.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), [serviceItem]).Value!;
        repo.WorkOrders.Add(woInProgress);

        var service = new PlatformYardMetricsService(repo);

        var from = DateTimeOffset.UtcNow.AddDays(-5);
        var to = DateTimeOffset.UtcNow.AddDays(1);

        var result = await service.GetYardMetricsAsync(from, to, tenantId);

        Assert.Equal(2, result.TotalAttendedVehicles);
        Assert.Equal(1, result.InProgressWorkOrders);
        Assert.NotEmpty(result.DailyVolume);
    }
}
