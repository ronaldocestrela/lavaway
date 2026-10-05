namespace CarWashSaaS.Shared.Contracts;

public sealed record WorkOrderNotificationSummaryDto(
    Guid WorkOrderId,
    bool ReceiptSent,
    DateTimeOffset? ReceiptSentAtUtc,
    string? ReceiptStatus,
    bool ReadyNoticeSent,
    DateTimeOffset? ReadyNoticeSentAtUtc,
    string? ReadyNoticeStatus,
    bool ComparisonPhotosSent,
    DateTimeOffset? ComparisonPhotosSentAtUtc,
    string? ComparisonPhotosStatus,
    IReadOnlyList<WhatsAppMessageDto> RecentMessages);
