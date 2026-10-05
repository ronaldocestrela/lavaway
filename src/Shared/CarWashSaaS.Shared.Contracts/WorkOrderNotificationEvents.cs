namespace CarWashSaaS.Shared.Contracts;

public static class WorkOrderNotificationEvents
{
    public const string ReceiptRequested = "work_order.receipt.requested";
    public const string ReadyForPickup = "work_order.ready.requested";
    public const string ComparisonPhotosRequested = "work_order.comparison_photos.requested";
}

public sealed record WorkOrderReceiptEventPayload(
    Guid WorkOrderId,
    string? CustomMessage = null);

public sealed record WorkOrderReadyEventPayload(
    Guid WorkOrderId,
    string? CustomMessage = null);

public sealed record WorkOrderComparisonPhotosEventPayload(
    Guid WorkOrderId,
    IReadOnlyList<Guid>? PhotoIds = null,
    string? CustomMessage = null);
