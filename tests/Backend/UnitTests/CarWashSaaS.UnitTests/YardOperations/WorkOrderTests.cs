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

    [Fact]
    public void Create_ShouldSetNotesAndEstimatedCompletion()
    {
        var tenantId = Guid.CreateVersion7();
        var item = WorkOrderItem.Create(tenantId, Guid.CreateVersion7(), "Polimento", 200m, 90).Value!;

        var result = WorkOrder.Create(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), [item], "Cliente solicitou cuidado com retrovisor.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Cliente solicitou cuidado com retrovisor.", result.Value!.Notes);
        Assert.Equal(result.Value.CreatedAtUtc.AddMinutes(90), result.Value.EstimatedCompletionAtUtc);
    }

    [Fact]
    public void Create_ShouldRejectNotesLongerThan500Characters()
    {
        var tenantId = Guid.CreateVersion7();
        var item = WorkOrderItem.Create(tenantId, Guid.CreateVersion7(), "Lavagem", 50m, 30).Value!;
        var longNotes = new string('A', 501);

        var result = WorkOrder.Create(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), [item], longNotes);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.notes.invalid", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_ShouldRejectDuplicateServicesInSameWorkOrder()
    {
        var tenantId = Guid.CreateVersion7();
        var serviceId = Guid.CreateVersion7();
        var item1 = WorkOrderItem.Create(tenantId, serviceId, "Lavagem Simples", 50m, 30).Value!;
        var item2 = WorkOrderItem.Create(tenantId, serviceId, "Lavagem Simples", 50m, 30).Value!;

        var result = WorkOrder.Create(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), [item1, item2]);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.duplicate_service", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }
}

