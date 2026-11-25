using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Tenants.Infrastructure;

public sealed class CampaignLinkRepository(TenantsDbContext dbContext) : ICampaignLinkRepository
{
    public async Task<CampaignLink?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.CampaignLinks
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<CampaignLink?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await dbContext.CampaignLinks
            .FirstOrDefaultAsync(c => c.Slug == slug, ct);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await dbContext.CampaignLinks
            .AnyAsync(c => c.Slug == slug, ct);
    }

    public async Task<IReadOnlyList<CampaignLink>> ListAsync(
        string? searchTerm = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = dbContext.CampaignLinks.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.Slug.Contains(term) || c.UtmCampaign.Contains(term));
        }

        var list = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        return list;
    }

    public async Task AddAsync(CampaignLink campaign, CancellationToken ct = default)
    {
        await dbContext.CampaignLinks.AddAsync(campaign, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public void Update(CampaignLink campaign)
    {
        dbContext.CampaignLinks.Update(campaign);
        dbContext.SaveChanges();
    }

    public async Task<bool> IncrementClickCountAsync(Guid id, DateTimeOffset utcNow, CancellationToken ct = default)
    {
        var rows = await dbContext.CampaignLinks
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ClickCount, c => c.ClickCount + 1)
                .SetProperty(c => c.UpdatedAtUtc, utcNow), ct);

        return rows > 0;
    }
}
