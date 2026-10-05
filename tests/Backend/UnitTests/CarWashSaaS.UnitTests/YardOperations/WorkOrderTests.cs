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

    [Fact]
    public void MarkPaymentConfirmed_ShouldSetPaymentProperties_AndAddHistory()
    {
        var tenantId = Guid.CreateVersion7();
        var item = WorkOrderItem.Create(tenantId, Guid.CreateVersion7(), "Lavagem", 80m, 30).Value!;
        var workOrder = WorkOrder.Create(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), [item]).Value!;
        var paidAtUtc = DateTimeOffset.UtcNow;

        var result = workOrder.MarkPaymentConfirmed(80m, "Pix", paidAtUtc, "TX-12345");

        Assert.True(result.IsSuccess);
        Assert.True(workOrder.IsPaid);
        Assert.Equal(80m, workOrder.PaidAmount);
        Assert.Equal("Pix", workOrder.PaymentMethod);
        Assert.Equal("TX-12345", workOrder.PaymentTransactionId);
        Assert.Equal(paidAtUtc, workOrder.PaidAtUtc);
        Assert.Contains(workOrder.StatusHistory, h => h.Notes != null && h.Notes.Contains("Pagamento confirmado via Pix"));
    }

    [Fact]
    public void MarkPaymentConfirmed_WhenAlreadyPaid_ShouldBeIdempotent()
    {
        var tenantId = Guid.CreateVersion7();
        var item = WorkOrderItem.Create(tenantId, Guid.CreateVersion7(), "Lavagem", 80m, 30).Value!;
        var workOrder = WorkOrder.Create(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), [item]).Value!;
        var paidAtUtc = DateTimeOffset.UtcNow;

        var firstResult = workOrder.MarkPaymentConfirmed(80m, "Pix", paidAtUtc, "TX-12345");
        Assert.True(firstResult.IsSuccess);
        var historyCount = workOrder.StatusHistory.Count;

        // Repetir a mesma confirmação
        var secondResult = workOrder.MarkPaymentConfirmed(80m, "Pix", paidAtUtc.AddMinutes(5), "TX-12345");

        Assert.True(secondResult.IsSuccess);
        Assert.Equal(historyCount, workOrder.StatusHistory.Count);
        Assert.Equal(paidAtUtc, workOrder.PaidAtUtc); // manteve o carimbo original
    }

    [Theory]
    [InlineData(0, "Pix")]
    [InlineData(-10, "Pix")]
    [InlineData(80, "")]
    [InlineData(80, "   ")]
    public void MarkPaymentConfirmed_ShouldRejectInvalidInputs(decimal amount, string method)
    {
        var tenantId = Guid.CreateVersion7();
        var item = WorkOrderItem.Create(tenantId, Guid.CreateVersion7(), "Lavagem", 80m, 30).Value!;
        var workOrder = WorkOrder.Create(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), [item]).Value!;

        var result = workOrder.MarkPaymentConfirmed(amount, method, DateTimeOffset.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.False(workOrder.IsPaid);
    }
}


