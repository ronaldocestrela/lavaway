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
    IReadOnlyCollection<WorkOrderItemDto> Items,
    Guid? AssignedOperatorId = null,
    string? AssignedOperatorName = null,
    IReadOnlyCollection<WorkOrderStatusHistoryDto>? StatusHistory = null,
    DateTimeOffset? PickedUpAtUtc = null,
    DateTimeOffset? SurveySentAtUtc = null,
    int? SurveyRating = null,
    DateTimeOffset? SurveyRespondedAtUtc = null);

public sealed record WorkOrderItemDto(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    decimal UnitPrice,
    int EstimatedDurationMinutes,
    int Quantity,
    decimal TotalAmount,
    int TotalDurationMinutes);
