namespace CarWashSaaS.Shared.Contracts;

public sealed record SendWorkOrderNotificationRequest(
    string? CustomMessage = null);

public sealed record SendComparisonPhotosRequest(
    IReadOnlyList<Guid>? SelectedPhotoIds = null,
    string? CustomMessage = null);
