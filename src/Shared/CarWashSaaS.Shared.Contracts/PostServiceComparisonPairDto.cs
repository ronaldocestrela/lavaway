namespace CarWashSaaS.Shared.Contracts;

public sealed record PostServiceComparisonPairDto(
    Guid? WorkOrderItemId,
    string? ServiceName,
    InspectionPhotoCategory Category,
    string Title,
    InspectionPhotoDto? BeforePhoto,
    PostServicePhotoDto AfterPhoto,
    string? Notes = null);
