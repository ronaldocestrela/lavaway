namespace CarWashSaaS.Billing.Domain;

public interface IProcessedSaasWebhookEventRepository
{
    Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken ct = default);
    Task<IReadOnlyList<ProcessedSaasWebhookEvent>> ListInPeriodAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProcessedSaasWebhookEvent>>([]);
    Task AddAsync(ProcessedSaasWebhookEvent webhookEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
