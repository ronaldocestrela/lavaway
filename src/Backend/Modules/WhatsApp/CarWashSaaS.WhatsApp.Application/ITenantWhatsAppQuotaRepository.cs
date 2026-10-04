using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public interface ITenantWhatsAppQuotaRepository
{
    Task<TenantWhatsAppQuota> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
