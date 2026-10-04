using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.WhatsApp.Application;

public interface IWhatsAppMessageSender
{
    Task<Result<string>> SendTextMessageAsync(Guid tenantId, string recipientPhone, string messageText, CancellationToken ct = default);
}
