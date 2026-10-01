using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.YardOperations.Infrastructure;

public sealed class TeamMemberRepository(YardOperationsDbContext dbContext) : ITeamMemberRepository
{
    public async Task<IReadOnlyCollection<TeamMember>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.TeamMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId)
            .OrderBy(member => member.FullName)
            .ToListAsync(ct);
    }

    public async Task<TeamMember?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await dbContext.TeamMembers
            .FirstOrDefaultAsync(member => member.TenantId == tenantId && member.Id == id, ct);
    }

    public async Task<TeamMember?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim();
        return await dbContext.TeamMembers
            .FirstOrDefaultAsync(member => member.TenantId == tenantId && member.Email == normalizedEmail, ct);
    }

    public async Task AddAsync(TeamMember teamMember, CancellationToken ct = default)
    {
        await dbContext.TeamMembers.AddAsync(teamMember, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TeamMember teamMember, CancellationToken ct = default)
    {
        dbContext.TeamMembers.Update(teamMember);
        await dbContext.SaveChangesAsync(ct);
    }
}
