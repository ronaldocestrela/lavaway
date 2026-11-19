using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class CustomRoleRepository(IdentityModuleDbContext context) : ICustomRoleRepository
{
    public async Task<CustomRole?> GetByIdAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
    {
        return await context.CustomRoles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == roleId, ct);
    }

    public async Task<CustomRole?> GetByNameAsync(Guid tenantId, string name, CancellationToken ct = default)
    {
        return await context.CustomRoles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == name, ct);
    }

    public async Task<IReadOnlyCollection<CustomRole>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await context.CustomRoles
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => !r.IsSystemRole)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, int>> CountUsersPerRoleAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await context.Users
            .Where(u => u.TenantId == tenantId && u.CustomRoleId.HasValue)
            .GroupBy(u => u.CustomRoleId!.Value)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);
    }

    public async Task<bool> HasAssignedUsersAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
    {
        return await context.Users
            .AnyAsync(u => u.TenantId == tenantId && u.CustomRoleId == roleId, ct);
    }

    public async Task AddAsync(CustomRole role, CancellationToken ct = default)
    {
        await context.CustomRoles.AddAsync(role, ct);
    }

    public void Update(CustomRole role)
    {
        context.CustomRoles.Update(role);
    }

    public void Remove(CustomRole role)
    {
        context.CustomRoles.Remove(role);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return context.SaveChangesAsync(ct);
    }
}
