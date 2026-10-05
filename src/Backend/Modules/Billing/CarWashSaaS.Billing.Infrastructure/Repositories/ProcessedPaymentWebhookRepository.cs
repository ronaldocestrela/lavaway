using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class ProcessedPaymentWebhookRepository(BillingDbContext dbContext) : IProcessedPaymentWebhookRepository
{
    public Task<ProcessedPaymentWebhook?> GetByEventIdAsync(Guid tenantId, string provider, string eventId, CancellationToken ct = default) =>
        dbContext.ProcessedPaymentWebhooks
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Provider == provider && w.EventId == eventId, ct);

    public Task<bool> HasBeenProcessedAsync(Guid tenantId, string provider, string eventId, CancellationToken ct = default) =>
        dbContext.ProcessedPaymentWebhooks
            .AnyAsync(w => w.TenantId == tenantId && w.Provider == provider && w.EventId == eventId, ct);

    public async Task AddAsync(ProcessedPaymentWebhook webhook, CancellationToken ct = default) =>
        await dbContext.ProcessedPaymentWebhooks.AddAsync(webhook, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);
}
