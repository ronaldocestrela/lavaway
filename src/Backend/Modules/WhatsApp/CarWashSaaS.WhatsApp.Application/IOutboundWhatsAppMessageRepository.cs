using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public interface IOutboundWhatsAppMessageRepository
{
    Task<OutboundWhatsAppMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OutboundWhatsAppMessage?> GetByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken ct = default);
    Task<OutboundWhatsAppMessage?> GetByProviderMessageIdAsync(Guid tenantId, string providerMessageId, CancellationToken ct = default);
    Task<IReadOnlyList<OutboundWhatsAppMessage>> GetRecentAsync(Guid tenantId, int count = 20, CancellationToken ct = default);
    Task AddAsync(OutboundWhatsAppMessage message, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
