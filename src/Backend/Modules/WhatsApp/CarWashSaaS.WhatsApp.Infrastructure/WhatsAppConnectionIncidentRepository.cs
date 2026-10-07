using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class WhatsAppConnectionIncidentRepository(WhatsAppDbContext dbContext) : IWhatsAppConnectionIncidentRepository
{
    public async Task AddAsync(WhatsAppConnectionIncident incident, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ct.ThrowIfCancellationRequested();

        await dbContext.WhatsAppConnectionIncidents.AddAsync(incident, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WhatsAppConnectionIncident>> ListRecentByTenantAsync(Guid tenantId, int count = 20, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var safeCount = count <= 0 ? 20 : (count > 100 ? 100 : count);

        return await dbContext.WhatsAppConnectionIncidents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.OccurredAtUtc)
            .Take(safeCount)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WhatsAppConnectionIncident>> ListRecentGlobalAsync(int count = 50, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var safeCount = count <= 0 ? 50 : (count > 200 ? 200 : count);

        return await dbContext.WhatsAppConnectionIncidents
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(i => i.OccurredAtUtc)
            .Take(safeCount)
            .ToListAsync(ct);
    }
}
