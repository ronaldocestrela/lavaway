using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class OutboundWhatsAppMessageRepository(WhatsAppDbContext dbContext) : IOutboundWhatsAppMessageRepository
{
    public async Task<OutboundWhatsAppMessage?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.OutboundWhatsAppMessages
            .Include(m => m.DeliveryAttempts)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<OutboundWhatsAppMessage?> GetByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken ct = default)
    {
        return await dbContext.OutboundWhatsAppMessages
            .Include(m => m.DeliveryAttempts)
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task<OutboundWhatsAppMessage?> GetByProviderMessageIdAsync(Guid tenantId, string providerMessageId, CancellationToken ct = default)
    {
        return await dbContext.OutboundWhatsAppMessages
            .Include(m => m.DeliveryAttempts)
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProviderMessageId == providerMessageId, ct);
    }

    public async Task<IReadOnlyList<OutboundWhatsAppMessage>> GetRecentAsync(Guid tenantId, int count = 20, CancellationToken ct = default)
    {
        return await dbContext.OutboundWhatsAppMessages
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OutboundWhatsAppMessage>> ListForPlatformMetricsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, CancellationToken ct = default)
    {
        var query = dbContext.OutboundWhatsAppMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.CreatedAt >= fromUtc && m.CreatedAt <= toUtc);

        if (tenantId.HasValue)
        {
            query = query.Where(m => m.TenantId == tenantId.Value);
        }

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
    }

    public async Task AddAsync(OutboundWhatsAppMessage message, CancellationToken ct = default)
    {
        await dbContext.OutboundWhatsAppMessages.AddAsync(message, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}
