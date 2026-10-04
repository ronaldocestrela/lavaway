using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.UnitTests.YardOperations;

public sealed class WorkOrderKanbanStatusTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid VehicleId = Guid.CreateVersion7();

    private static WorkOrder CreateTestWorkOrder()
    {
        var item = WorkOrderItem.Create(TenantId, Guid.CreateVersion7(), "Lavagem Detalhada", 100m, 45).Value!;
        return WorkOrder.Create(TenantId, CustomerId, VehicleId, [item]).Value!;
    }

    [Fact]
    public void Create_ShouldInitializeWithWaitingStatusAndInitialStatusHistory()
    {
        var order = CreateTestWorkOrder();

        Assert.Equal(WorkOrderStatus.Waiting, order.Status);
        Assert.Null(order.AssignedOperatorId);
        Assert.Null(order.AssignedOperatorName);
        Assert.Single(order.StatusHistory);

        var initialHistory = order.StatusHistory.First();
        Assert.Null(initialHistory.FromStatus);
        Assert.Equal(WorkOrderStatus.Waiting, initialHistory.ToStatus);
        Assert.Equal(order.CreatedAtUtc, initialHistory.ChangedAtUtc);
    }

    [Fact]
    public void AssignOperator_ShouldUpdateOperatorDetails()
    {
        var order = CreateTestWorkOrder();
        var operatorId = Guid.CreateVersion7();

        var result = order.AssignOperator(operatorId, "Carlos Operador");

        Assert.True(result.IsSuccess);
        Assert.Equal(operatorId, order.AssignedOperatorId);
        Assert.Equal("Carlos Operador", order.AssignedOperatorName);
    }

    [Fact]
    public void AssignOperator_WithNull_ShouldClearOperator()
    {
        var order = CreateTestWorkOrder();
        order.AssignOperator(Guid.CreateVersion7(), "Carlos Operador");

        var result = order.AssignOperator(null, null);

        Assert.True(result.IsSuccess);
        Assert.Null(order.AssignedOperatorId);
        Assert.Null(order.AssignedOperatorName);
    }

    [Fact]
    public void ChangeStatus_ShouldFollowStandardForwardFlow()
    {
        var order = CreateTestWorkOrder();
        var operatorId = Guid.CreateVersion7();

        // 1. Waiting -> InWashing
        var step1 = order.ChangeStatus(WorkOrderStatus.InWashing, operatorId, "Lavador João");
        Assert.True(step1.IsSuccess);
        Assert.Equal(WorkOrderStatus.InWashing, order.Status);
        Assert.Equal(operatorId, order.AssignedOperatorId);
        Assert.Equal("Lavador João", order.AssignedOperatorName);

        // 2. InWashing -> Finishing
        var step2 = order.ChangeStatus(WorkOrderStatus.Finishing);
        Assert.True(step2.IsSuccess);
        Assert.Equal(WorkOrderStatus.Finishing, order.Status);

        // 3. Finishing -> QualityControl
        var step3 = order.ChangeStatus(WorkOrderStatus.QualityControl);
        Assert.True(step3.IsSuccess);
        Assert.Equal(WorkOrderStatus.QualityControl, order.Status);

        // 4. QualityControl -> ReadyForPickup
        var step4 = order.ChangeStatus(WorkOrderStatus.ReadyForPickup);
        Assert.True(step4.IsSuccess);
        Assert.Equal(WorkOrderStatus.ReadyForPickup, order.Status);

        // Total history records: Initial + 4 transitions = 5
        Assert.Equal(5, order.StatusHistory.Count);
    }

    [Fact]
    public void ChangeStatus_ShouldRejectIllegalStatusSkips()
    {
        var order = CreateTestWorkOrder();

        // Cannot skip directly from Waiting to ReadyForPickup
        var result = order.ChangeStatus(WorkOrderStatus.ReadyForPickup);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.invalid_status_transition", result.Error!.Code);
        Assert.Equal(WorkOrderStatus.Waiting, order.Status);
    }

    [Fact]
    public void ChangeStatus_ToSameStatus_ShouldFail()
    {
        var order = CreateTestWorkOrder();

        var result = order.ChangeStatus(WorkOrderStatus.Waiting);

        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.same_status", result.Error!.Code);
    }

    [Fact]
    public void ChangeStatus_FromQualityControl_BackToFinishing_RequiresNotes()
    {
        var order = CreateTestWorkOrder();
        order.ChangeStatus(WorkOrderStatus.InWashing);
        order.ChangeStatus(WorkOrderStatus.Finishing);
        order.ChangeStatus(WorkOrderStatus.QualityControl);

        // Without notes: fails
        var failResult = order.ChangeStatus(WorkOrderStatus.Finishing, notes: null);
        Assert.False(failResult.IsSuccess);
        Assert.Equal("work_order.rework_notes_required", failResult.Error!.Code);

        // With notes: succeeds
        var successResult = order.ChangeStatus(WorkOrderStatus.Finishing, notes: "Vidro traseiro com manchas de água; refazer secagem");
        Assert.True(successResult.IsSuccess);
        Assert.Equal(WorkOrderStatus.Finishing, order.Status);

        var lastHistory = order.StatusHistory.Last();
        Assert.Equal(WorkOrderStatus.QualityControl, lastHistory.FromStatus);
        Assert.Equal(WorkOrderStatus.Finishing, lastHistory.ToStatus);
        Assert.Equal("Vidro traseiro com manchas de água; refazer secagem", lastHistory.Notes);
    }

    [Fact]
    public void ChangeStatus_FromReadyForPickup_ShouldBeTerminal()
    {
        var order = CreateTestWorkOrder();
        order.ChangeStatus(WorkOrderStatus.InWashing);
        order.ChangeStatus(WorkOrderStatus.Finishing);
        order.ChangeStatus(WorkOrderStatus.QualityControl);
        order.ChangeStatus(WorkOrderStatus.ReadyForPickup);

        var result = order.ChangeStatus(WorkOrderStatus.InWashing, notes: "Tentativa de reabrir");
        Assert.False(result.IsSuccess);
        Assert.Equal("work_order.already_ready_for_pickup", result.Error!.Code);
    }
}
