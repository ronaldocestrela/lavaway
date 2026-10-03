namespace CarWashSaaS.Shared.Contracts;

public sealed record WorkOrderDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string Plate,
    string VehicleSize,
    string Status,
    decimal TotalAmount,
    int EstimatedDurationMinutes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset EstimatedCompletionAtUtc,
    string? Notes,
    IReadOnlyCollection<WorkOrderItemDto> Items);

public sealed record WorkOrderItemDto(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    decimal UnitPrice,
    int EstimatedDurationMinutes,
    int Quantity,
    decimal TotalAmount,
    int TotalDurationMinutes);
