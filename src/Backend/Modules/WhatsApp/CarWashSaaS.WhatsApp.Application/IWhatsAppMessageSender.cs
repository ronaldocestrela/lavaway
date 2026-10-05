using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Application;

public interface IWhatsAppMessageSender
{
    Task<Result<string>> SendTextMessageAsync(Guid tenantId, string recipientPhone, string messageText, CancellationToken ct = default);

    Task<Result<string>> SendMediaMessageAsync(
        Guid tenantId,
        string recipientPhone,
        string mediaBase64OrUrl,
        string mediaType,
        string mimeType,
        string fileName,
        string? caption = null,
        CancellationToken ct = default);
}

