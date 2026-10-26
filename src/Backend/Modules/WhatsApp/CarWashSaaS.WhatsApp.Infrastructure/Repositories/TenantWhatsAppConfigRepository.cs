using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.WhatsApp.Infrastructure.Repositories;

public sealed class TenantWhatsAppConfigRepository(WhatsAppDbContext dbContext) : ITenantWhatsAppConfigRepository
{
    public async Task<TenantWhatsAppConfig?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.TenantWhatsAppConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
    }

    public async Task AddAsync(TenantWhatsAppConfig config, CancellationToken ct = default)
    {
        await dbContext.TenantWhatsAppConfigs.AddAsync(config, ct);
    }

    public void Update(TenantWhatsAppConfig config)
    {
        dbContext.TenantWhatsAppConfigs.Update(config);
    }
}
