using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public interface ITenantWhatsAppConfigRepository
{
    Task<TenantWhatsAppConfig?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantWhatsAppConfig config, CancellationToken ct = default);
    void Update(TenantWhatsAppConfig config);
}
