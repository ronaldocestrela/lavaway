namespace CarWashSaaS.Shared.Contracts;

public sealed record WorkOrderMovedNotification(
    Guid WorkOrderId,
    string? FromStatus,
    string ToStatus,
    Guid? OperatorId,
    string? OperatorName,
    DateTimeOffset ChangedAtUtc,
    string? Notes);
