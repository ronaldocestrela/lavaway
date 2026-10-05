using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Application;

public interface IWorkOrderReceiptPdfGenerator
{
    Result<byte[]> GenerateReceiptPdf(WorkOrderReceiptPdfModel model);
}

public sealed record WorkOrderReceiptPdfModel(
    Guid TenantId,
    Guid WorkOrderId,
    string StoreTradeName,
    string? StorePhone,
    string? StoreAddress,
    string CustomerName,
    string CustomerPhone,
    string VehiclePlate,
    string VehicleModel,
    string VehicleSize,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset EstimatedCompletionAtUtc,
    decimal TotalAmount,
    string? Notes,
    IReadOnlyList<WorkOrderReceiptItemModel> Items,
    WorkOrderReceiptInspectionModel? Inspection);

public sealed record WorkOrderReceiptItemModel(
    string ServiceName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount);

public sealed record WorkOrderReceiptInspectionModel(
    int? OdometerKm,
    string? FuelLevel,
    IReadOnlyList<WorkOrderReceiptChecklistItemModel> ChecklistItems,
    IReadOnlyList<WorkOrderReceiptDamageModel> Damages);

public sealed record WorkOrderReceiptChecklistItemModel(
    string Title,
    string Status,
    string? Observation);

public sealed record WorkOrderReceiptDamageModel(
    string Type,
    string View,
    string Severity,
    string? Description);
