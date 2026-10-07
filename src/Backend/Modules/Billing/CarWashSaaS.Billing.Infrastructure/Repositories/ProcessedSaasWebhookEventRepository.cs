using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class ProcessedSaasWebhookEventRepository(BillingDbContext dbContext) : IProcessedSaasWebhookEventRepository
{
    public async Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken ct = default)
    {
        return await dbContext.ProcessedSaasWebhookEvents
            .AnyAsync(e => e.EventId == eventId, ct);
    }

    public async Task AddAsync(ProcessedSaasWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        await dbContext.ProcessedSaasWebhookEvents.AddAsync(webhookEvent, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
