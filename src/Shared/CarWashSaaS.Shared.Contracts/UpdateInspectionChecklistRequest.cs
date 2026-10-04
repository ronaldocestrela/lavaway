namespace CarWashSaaS.Shared.Contracts;

public sealed record UpdateChecklistItemRequest(
    string ItemKey,
    string Title,
    ChecklistItemStatus Status,
    string? Observation = null);

public sealed record UpdateInspectionChecklistRequest(
    string? FuelLevel,
    int? OdometerKm,
    string? Notes,
    IReadOnlyCollection<UpdateChecklistItemRequest> Items);
