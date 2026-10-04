namespace CarWashSaaS.Shared.Contracts;

public sealed record VehicleInspectionDto(
    Guid Id,
    Guid TenantId,
    Guid WorkOrderId,
    Guid VehicleId,
    string? FuelLevel,
    int? OdometerKm,
    string? Notes,
    InspectionStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyCollection<InspectionDamageDto> Damages,
    IReadOnlyCollection<InspectionChecklistItemDto> ChecklistItems,
    IReadOnlyCollection<InspectionPhotoDto> Photos);
