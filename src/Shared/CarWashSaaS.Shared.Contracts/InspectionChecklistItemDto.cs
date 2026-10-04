namespace CarWashSaaS.Shared.Contracts;

public sealed record InspectionChecklistItemDto(
    Guid Id,
    string ItemKey,
    string Title,
    ChecklistItemStatus Status,
    string? Observation);
