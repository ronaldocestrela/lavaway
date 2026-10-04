namespace CarWashSaaS.Shared.Contracts;

public sealed record AddPostServicePhotoRequest(
    Guid? WorkOrderItemId = null,
    Guid? BeforeInspectionPhotoId = null,
    InspectionPhotoCategory Category = InspectionPhotoCategory.Other,
    string? Title = null,
    string? Notes = null);
