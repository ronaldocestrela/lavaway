using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class TenantWhatsAppQuotaRepository(WhatsAppDbContext dbContext) : ITenantWhatsAppQuotaRepository
{
    public async Task<TenantWhatsAppQuota> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default)
    {
        var quota = await dbContext.TenantWhatsAppQuotas
            .FirstOrDefaultAsync(q => q.TenantId == tenantId, ct);

        if (quota is null)
        {
            var defaultQuota = TenantWhatsAppQuota.CreateDefault(tenantId).Value!;
            await dbContext.TenantWhatsAppQuotas.AddAsync(defaultQuota, ct);
            await dbContext.SaveChangesAsync(ct);
            return defaultQuota;
        }

        return quota;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}
