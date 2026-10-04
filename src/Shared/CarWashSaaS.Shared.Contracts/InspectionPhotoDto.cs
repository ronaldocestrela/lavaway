namespace CarWashSaaS.Shared.Contracts;

public sealed record InspectionPhotoDto(
    Guid Id,
    InspectionPhotoCategory Category,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid? DamageId,
    DateTimeOffset UploadedAtUtc);
