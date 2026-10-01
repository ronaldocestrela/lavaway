using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class WorkOrderTests
{
    [Fact]
    public void Create_ShouldSnapshotItemsAndCalculateTotalAndDuration()
    {
        var tenantId = Guid.CreateVersion7();
        var serviceId = Guid.CreateVersion7();
        var item = WorkOrderItem.Create(tenantId, serviceId, "Lavagem completa", 120m, 60).Value!;

        var result = WorkOrder.Create(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), [item]);

        Assert.True(result.IsSuccess);
        Assert.Equal(120m, result.Value!.TotalAmount);
        Assert.Equal(60, result.Value.EstimatedDurationMinutes);
        Assert.Equal("Lavagem completa", result.Value.Items.Single().ServiceName);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal(WorkOrderStatus.Waiting, result.Value.Status);
    }

    [Fact]
    public void Create_ShouldRejectEmptyItems()
    {
        var result = WorkOrder.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), []);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}