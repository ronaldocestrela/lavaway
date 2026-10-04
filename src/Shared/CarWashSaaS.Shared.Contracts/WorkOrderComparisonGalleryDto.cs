namespace CarWashSaaS.Shared.Contracts;

public sealed record WorkOrderComparisonGalleryDto(
    Guid WorkOrderId,
    string CustomerName,
    string Plate,
    string VehicleSize,
    string Status,
    bool IsEligible,
    IReadOnlyList<WorkOrderItemDto> EligibleServices,
    IReadOnlyList<PostServiceComparisonPairDto> ComparisonPairs,
    IReadOnlyList<InspectionPhotoDto> UnpairedBeforePhotos,
    IReadOnlyList<PostServicePhotoDto> UnpairedAfterPhotos);
