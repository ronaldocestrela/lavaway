namespace CarWashSaaS.Shared.Contracts;

public sealed record PostServicePhotoDto(
    Guid Id,
    Guid WorkOrderId,
    Guid? WorkOrderItemId,
    string? ServiceName,
    Guid? BeforeInspectionPhotoId,
    InspectionPhotoCategory Category,
    string Title,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc,
    string? Notes = null);
