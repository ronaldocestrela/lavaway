namespace CarWashSaaS.Shared.Contracts;

public sealed record WorkOrderKanbanCardDto(
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
    Guid? AssignedOperatorId,
    string? AssignedOperatorName,
    string? Notes,
    IReadOnlyList<string> ServiceNames,
    int ItemsCount,
    DateTimeOffset LastStatusChangedAtUtc);

public sealed record YardKanbanColumnDto(
    string Status,
    string Title,
    int Count,
    IReadOnlyList<WorkOrderKanbanCardDto> Cards);

public sealed record YardKanbanBoardDto(
    IReadOnlyList<YardKanbanColumnDto> Columns,
    int TotalActiveOrders,
    int CapacityTotalBoxes,
    int OccupiedBoxes);
