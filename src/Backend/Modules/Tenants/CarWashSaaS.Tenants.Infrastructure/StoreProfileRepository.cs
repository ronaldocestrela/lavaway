using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Tenants.Infrastructure;

public sealed class StoreProfileRepository(TenantsDbContext dbContext) : IStoreProfileRepository
{
    public async Task<StoreProfile?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.StoreProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.TenantId == tenantId, ct);
    }

    public async Task AddAsync(StoreProfile profile, CancellationToken ct = default)
    {
        await dbContext.StoreProfiles.AddAsync(profile, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(StoreProfile profile, CancellationToken ct = default)
    {
        dbContext.StoreProfiles.Update(profile);
        await dbContext.SaveChangesAsync(ct);
    }
}
