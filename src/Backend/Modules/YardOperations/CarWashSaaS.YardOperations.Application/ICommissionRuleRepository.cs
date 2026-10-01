using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface ICommissionRuleRepository
{
    Task<IReadOnlyCollection<CommissionRule>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<CommissionRule?> GetByServiceAndRoleAsync(Guid tenantId, string serviceName, string roleName, CancellationToken ct = default);
    Task AddAsync(CommissionRule commissionRule, CancellationToken ct = default);
}
