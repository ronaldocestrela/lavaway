using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Identity.Infrastructure;

public sealed class AdministrativeAuditEventRepository(
    IdentityModuleDbContext dbContext) : IAdministrativeAuditEventRepository
{
    public async Task AddAsync(AdministrativeAuditEvent auditEvent, CancellationToken ct = default)
    {
        await dbContext.AdministrativeAuditEvents.AddAsync(auditEvent, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<AdministrativeAuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.AdministrativeAuditEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<PagedResult<AdministrativeAuditEvent>> SearchAsync(
        AuditQueryFilter filter,
        CancellationToken ct = default)
    {
        var query = dbContext.AdministrativeAuditEvents
            .AsNoTracking()
            .AsQueryable();

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(e => e.TimestampUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(e => e.TimestampUtc <= filter.ToUtc.Value);
        }

        if (filter.ActorId.HasValue && filter.ActorId.Value != Guid.Empty)
        {
            query = query.Where(e => e.ActorId == filter.ActorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActorEmail))
        {
            var email = filter.ActorEmail.Trim().ToLowerInvariant();
            query = query.Where(e => e.ActorEmail.Contains(email));
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim();
            query = query.Where(e => e.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetType))
        {
            var targetType = filter.TargetType.Trim();
            query = query.Where(e => e.TargetType == targetType);
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetId))
        {
            var targetId = filter.TargetId.Trim();
            query = query.Where(e => e.TargetId == targetId);
        }

        if (filter.TenantId.HasValue && filter.TenantId.Value != Guid.Empty)
        {
            query = query.Where(e => e.TenantId == filter.TenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Outcome))
        {
            var outcome = filter.Outcome.Trim();
            query = query.Where(e => e.Outcome == outcome);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(e =>
                e.ActorEmail.Contains(term) ||
                e.Action.Contains(term) ||
                e.TargetType.Contains(term) ||
                e.TargetId.Contains(term) ||
                e.DetailsJson.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var items = await query
            .OrderByDescending(e => e.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AdministrativeAuditEvent>(items, totalCount, page, pageSize);
    }
}
