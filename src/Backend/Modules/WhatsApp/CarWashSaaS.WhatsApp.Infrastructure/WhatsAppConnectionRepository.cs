using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.WhatsApp.Infrastructure;

public sealed class WhatsAppConnectionRepository(WhatsAppDbContext dbContext) : IWhatsAppConnectionRepository
{
    public async Task<WhatsAppConnection?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await dbContext.WhatsAppConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(connection => connection.TenantId == tenantId, ct);
    }

    public async Task AddAsync(WhatsAppConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ct.ThrowIfCancellationRequested();

        await dbContext.WhatsAppConnections.AddAsync(connection, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WhatsAppConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ct.ThrowIfCancellationRequested();

        dbContext.WhatsAppConnections.Update(connection);
        await dbContext.SaveChangesAsync(ct);
    }
}
