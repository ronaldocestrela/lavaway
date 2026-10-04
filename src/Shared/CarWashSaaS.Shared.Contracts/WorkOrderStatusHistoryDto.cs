namespace CarWashSaaS.Shared.Contracts;

public sealed record WorkOrderStatusHistoryDto(
    Guid Id,
    string? FromStatus,
    string ToStatus,
    DateTimeOffset ChangedAtUtc,
    Guid? ChangedByOperatorId,
    string? ChangedByOperatorName,
    string? Notes);
