using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class YardCapacityRepository(YardOperationsDbContext dbContext) : IYardCapacityRepository
{
    public async Task<YardCapacity?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.YardCapacities
            .AsNoTracking()
            .FirstOrDefaultAsync(capacity => capacity.TenantId == tenantId, ct);
    }

    public async Task AddAsync(YardCapacity yardCapacity, CancellationToken ct = default)
    {
        await dbContext.YardCapacities.AddAsync(yardCapacity, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(YardCapacity yardCapacity, CancellationToken ct = default)
    {
        dbContext.YardCapacities.Update(yardCapacity);
        await dbContext.SaveChangesAsync(ct);
    }
}
