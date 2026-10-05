using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class LoyaltyProgramRepository(YardOperationsDbContext dbContext) : ILoyaltyProgramRepository
{
    public Task<LoyaltyProgram?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
        dbContext.LoyaltyPrograms.FirstOrDefaultAsync(p => p.TenantId == tenantId, ct);

    public async Task AddAsync(LoyaltyProgram program, CancellationToken ct = default) =>
        await dbContext.LoyaltyPrograms.AddAsync(program, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);
}
