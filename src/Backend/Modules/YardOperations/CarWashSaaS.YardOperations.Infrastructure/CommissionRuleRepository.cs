using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class CommissionRuleRepository(YardOperationsDbContext dbContext) : ICommissionRuleRepository
{
    public async Task<IReadOnlyCollection<CommissionRule>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.CommissionRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId)
            .OrderBy(rule => rule.ServiceName)
            .ThenBy(rule => rule.RoleName)
            .ToListAsync(ct);
    }

    public async Task<CommissionRule?> GetByServiceAndRoleAsync(Guid tenantId, string serviceName, string roleName, CancellationToken ct = default)
    {
        return await dbContext.CommissionRules
            .FirstOrDefaultAsync(rule => rule.TenantId == tenantId && rule.ServiceName == serviceName && rule.RoleName == roleName, ct);
    }

    public async Task AddAsync(CommissionRule commissionRule, CancellationToken ct = default)
    {
        await dbContext.CommissionRules.AddAsync(commissionRule, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}
