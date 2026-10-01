using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class ServiceRepository(YardOperationsDbContext dbContext) : IServiceRepository
{
    public async Task<IReadOnlyCollection<Service>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.Services
            .Include(service => service.Prices)
            .AsNoTracking()
            .Where(service => service.TenantId == tenantId)
            .OrderBy(service => service.Category)
            .ThenBy(service => service.Name)
            .ToListAsync(ct);
    }

    public async Task<Service?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await dbContext.Services
            .Include(service => service.Prices)
            .FirstOrDefaultAsync(service => service.TenantId == tenantId && service.Id == id, ct);
    }

    public async Task AddAsync(Service service, CancellationToken ct = default)
    {
        await dbContext.Services.AddAsync(service, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Service service, CancellationToken ct = default)
    {
        dbContext.Services.Update(service);
        await dbContext.SaveChangesAsync(ct);
    }
}
