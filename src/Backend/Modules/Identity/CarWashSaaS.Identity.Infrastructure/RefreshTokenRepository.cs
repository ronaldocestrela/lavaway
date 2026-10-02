using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class RefreshTokenRepository(IdentityModuleDbContext context) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
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
        context.RefreshTokens.Update(token);
        await context.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAtUtc, CancellationToken ct = default)
    {
        var activeTokens = await context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.Revoke(revokedAtUtc);
        }

        if (activeTokens.Count > 0)
        {
            await context.SaveChangesAsync(ct);
        }
    }
}
