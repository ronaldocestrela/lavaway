using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface ITeamMemberRepository
{
    Task<IReadOnlyCollection<TeamMember>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TeamMember?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<TeamMember?> GetByEmailAsync(Guid tenantId, string? email, CancellationToken ct = default);
    Task AddAsync(TeamMember teamMember, CancellationToken ct = default);
    Task UpdateAsync(TeamMember teamMember, CancellationToken ct = default);
}
