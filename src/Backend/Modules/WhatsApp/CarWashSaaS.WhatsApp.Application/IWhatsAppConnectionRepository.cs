using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public interface IWhatsAppConnectionRepository
{
    Task<WhatsAppConnection?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(WhatsAppConnection connection, CancellationToken ct = default);
    Task UpdateAsync(WhatsAppConnection connection, CancellationToken ct = default);
}
