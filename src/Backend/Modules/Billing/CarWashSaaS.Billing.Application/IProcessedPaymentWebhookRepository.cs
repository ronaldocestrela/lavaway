using CarWashSaaS.Billing.Domain;

namespace CarWashSaaS.Billing.Application;

public interface IProcessedPaymentWebhookRepository
{
    Task<ProcessedPaymentWebhook?> GetByEventIdAsync(Guid tenantId, string provider, string eventId, CancellationToken ct = default);
    Task<bool> HasBeenProcessedAsync(Guid tenantId, string provider, string eventId, CancellationToken ct = default);
    Task AddAsync(ProcessedPaymentWebhook webhook, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
