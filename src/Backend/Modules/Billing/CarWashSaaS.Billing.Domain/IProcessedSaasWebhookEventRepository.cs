namespace CarWashSaaS.Billing.Domain;

public interface IProcessedSaasWebhookEventRepository
{
    Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken ct = default);
    Task AddAsync(ProcessedSaasWebhookEvent webhookEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
