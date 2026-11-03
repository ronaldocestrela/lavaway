namespace CarWashSaaS.Shared.Contracts;

public sealed record PublicInspectionDto(
    Guid WorkOrderId,
    string TrackingToken,
    string StoreTradeName,
    string? StorePhone,
    string? StoreAddress,
    string? StoreLogoUrl,
    string? StorePrimaryColor,
    string CustomerName,
    string VehiclePlate,
    string VehicleModel,
    string VehicleSize,
    DateTimeOffset CheckInAtUtc,
    string Status,
    int? OdometerKm,
    string? FuelLevel,
    string? InspectionNotes,
    DateTimeOffset? InspectionCompletedAtUtc,
    IReadOnlyList<PublicInspectionPhotoDto> Photos,
    IReadOnlyList<PublicInspectionDamageDto> Damages,
    IReadOnlyList<PublicInspectionChecklistItemDto> ChecklistItems
);

public sealed record PublicInspectionPhotoDto(
    Guid PhotoId,
    string Category,
    string CategoryLabel,
    string Url,
    bool IsPurged,
    DateTimeOffset UploadedAtUtc
);

public sealed record PublicInspectionDamageDto(
    string Type,
    string View,
    decimal CoordinateX,
    decimal CoordinateY,
    string Severity,
    string? Description
);

public sealed record PublicInspectionChecklistItemDto(
    string Title,
    string Status,
    string? Observation
);

