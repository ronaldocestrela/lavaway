using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class TenantPaymentGatewayConfigRepository(BillingDbContext dbContext) : ITenantPaymentGatewayConfigRepository
{
    public async Task<TenantPaymentGatewayConfig?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.TenantPaymentGatewayConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
    }

    public async Task AddAsync(TenantPaymentGatewayConfig config, CancellationToken ct = default)
    {
        await dbContext.TenantPaymentGatewayConfigs.AddAsync(config, ct);
    }

    public void Update(TenantPaymentGatewayConfig config)
    {
        dbContext.TenantPaymentGatewayConfigs.Update(config);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await dbContext.SaveChangesAsync(ct);
    }
}
