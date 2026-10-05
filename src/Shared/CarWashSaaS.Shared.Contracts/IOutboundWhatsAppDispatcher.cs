namespace CarWashSaaS.Shared.Contracts;

public interface IOutboundWhatsAppDispatcher
{
    Task<Result<WhatsAppMessageDto>> DispatchTextMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string messageText,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task<Result<WhatsAppMessageDto>> DispatchMediaMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string caption,
        string mediaType,
        string mediaUrlOrBase64,
        string mediaMimeType,
        string mediaFileName,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<WhatsAppMessageDto>>> GetRecentMessagesAsync(
        Guid tenantId,
        int count = 20,
        CancellationToken ct = default);
}
