using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Tenants.Infrastructure;

public sealed class StoreProfileRepository(
    TenantsDbContext dbContext,
    ICurrentTenantAccessor? currentTenantAccessor = null) : IStoreProfileRepository
{
    public async Task<StoreProfile?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.StoreProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.TenantId == tenantId, ct);
    }

    public async Task AddAsync(StoreProfile profile, CancellationToken ct = default)
    {
        EnsureTenantContext(profile.TenantId);
        await dbContext.StoreProfiles.AddAsync(profile, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(StoreProfile profile, CancellationToken ct = default)
    {
        EnsureTenantContext(profile.TenantId);
        dbContext.StoreProfiles.Update(profile);
        await dbContext.SaveChangesAsync(ct);
    }

    private void EnsureTenantContext(Guid tenantId)
    {
        if (currentTenantAccessor?.TenantId is null && currentTenantAccessor is CurrentTenantAccessor mutableAccessor)
        {
            mutableAccessor.SetTenant(tenantId);
        }
    }
}
