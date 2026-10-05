using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface ILoyaltyProgramRepository
{
    Task<LoyaltyProgram?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(LoyaltyProgram program, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
