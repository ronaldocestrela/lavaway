namespace CarWashSaaS.Shared.Contracts;

public sealed record WhatsAppMessageDto(
    Guid Id,
    string RecipientPhone,
    string MessageText,
    string Status,
    string? FailureReason,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    string? MediaType = null,
    string? MediaFileName = null);

