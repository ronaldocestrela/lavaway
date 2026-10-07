using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Tenants.Infrastructure;

public sealed class TenantRepository(TenantsDbContext dbContext) : ITenantRepository
{
    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<PagedResult<GlobalTenantSummaryDto>> SearchGlobalTenantsAsync(
        GetGlobalTenantsRequest request,
        CancellationToken ct = default)
    {
        var tenantsQuery = dbContext.Tenants.IgnoreQueryFilters().AsNoTracking();
        var profilesQuery = dbContext.StoreProfiles.IgnoreQueryFilters().AsNoTracking();

        if (request.Status.HasValue)
        {
            tenantsQuery = tenantsQuery.Where(t => t.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim();
            tenantsQuery = tenantsQuery.Where(t =>
                t.Name.Contains(search) ||
                profilesQuery.Any(p => p.TenantId == t.Id && (
                    p.TradeName.Contains(search) ||
                    p.LegalName.Contains(search) ||
                    p.Cnpj.Contains(search) ||
                    p.City.Contains(search))));
        }

        var totalCount = await tenantsQuery.CountAsync(ct);

        var query = from t in tenantsQuery
                    join p in profilesQuery on t.Id equals p.TenantId into profiles
                    from p in profiles.DefaultIfEmpty()
                    orderby t.CreatedAtUtc descending
                    select new GlobalTenantSummaryDto(
                        t.Id,
                        t.Name,
                        t.Status,
                        t.CreatedAtUtc,
                        t.StatusChangedAtUtc,
                        t.TrialEndsAtUtc,
                        t.StatusReason,
                        p != null ? p.TradeName : null,
                        p != null ? p.LegalName : null,
                        p != null ? p.Cnpj : null,
                        p != null ? p.Phone : null,
                        p != null ? p.City : null,
                        p != null ? p.State : null);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<GlobalTenantSummaryDto>(items, totalCount, request.Page, request.PageSize);
    }

    public async Task<GlobalTenantSummaryDto?> GetSummaryByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantsQuery = dbContext.Tenants.IgnoreQueryFilters().AsNoTracking().Where(t => t.Id == id);
        var profilesQuery = dbContext.StoreProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.TenantId == id);

        var query = from t in tenantsQuery
                    join p in profilesQuery on t.Id equals p.TenantId into profiles
                    from p in profiles.DefaultIfEmpty()
                    select new GlobalTenantSummaryDto(
                        t.Id,
                        t.Name,
                        t.Status,
                        t.CreatedAtUtc,
                        t.StatusChangedAtUtc,
                        t.TrialEndsAtUtc,
                        t.StatusReason,
                        p != null ? p.TradeName : null,
                        p != null ? p.LegalName : null,
                        p != null ? p.Cnpj : null,
                        p != null ? p.Phone : null,
                        p != null ? p.City : null,
                        p != null ? p.State : null);

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        await dbContext.Tenants.AddAsync(tenant, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken ct = default)
    {
        dbContext.Tenants.Update(tenant);
        await dbContext.SaveChangesAsync(ct);
    }
}
