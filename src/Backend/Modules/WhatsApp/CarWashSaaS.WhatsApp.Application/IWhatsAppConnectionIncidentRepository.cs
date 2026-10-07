using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public interface IWhatsAppConnectionIncidentRepository
{
    Task AddAsync(WhatsAppConnectionIncident incident, CancellationToken ct = default);
    Task<IReadOnlyList<WhatsAppConnectionIncident>> ListRecentByTenantAsync(Guid tenantId, int count = 20, CancellationToken ct = default);
    Task<IReadOnlyList<WhatsAppConnectionIncident>> ListRecentGlobalAsync(int count = 50, CancellationToken ct = default);
}
