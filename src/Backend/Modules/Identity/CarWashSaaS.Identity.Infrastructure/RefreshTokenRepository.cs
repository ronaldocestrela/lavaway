using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class RefreshTokenRepository(
    IdentityModuleDbContext context,
    ICurrentTenantAccessor currentTenantAccessor) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        EnsureTenantContext(token.TenantId);
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync(ct);
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return await context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }

    public async Task UpdateAsync(RefreshToken token, CancellationToken ct = default)
    {
        EnsureTenantContext(token.TenantId);
        context.RefreshTokens.Update(token);
        await context.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAtUtc, CancellationToken ct = default)
    {
        var activeTokens = await context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        if (activeTokens.Count > 0)
        {
            EnsureTenantContext(activeTokens[0].TenantId);
            foreach (var token in activeTokens)
            {
                token.Revoke(revokedAtUtc);
            }

            await context.SaveChangesAsync(ct);
        }
    }

    private void EnsureTenantContext(Guid tenantId)
    {
        if (currentTenantAccessor.TenantId is null && currentTenantAccessor is CurrentTenantAccessor mutableAccessor)
        {
            mutableAccessor.SetTenant(tenantId);
        }
    }
}
