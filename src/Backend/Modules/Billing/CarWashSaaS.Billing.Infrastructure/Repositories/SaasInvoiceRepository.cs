using CarWashSaaS.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class SaasInvoiceRepository(BillingDbContext dbContext) : ISaasInvoiceRepository
{
    public async Task<SaasInvoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.SaasInvoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<SaasInvoice?> GetByGatewayInvoiceIdAsync(string gatewayInvoiceId, CancellationToken ct = default)
    {
        return await dbContext.SaasInvoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.GatewayInvoiceId == gatewayInvoiceId, ct);
    }

    public async Task<IReadOnlyList<SaasInvoice>> ListByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.SaasInvoices
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.DueDateUtc)
            .ToListAsync(ct);
    }

    public async Task<SaasInvoice?> GetPendingByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await dbContext.SaasInvoices
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && (i.Status == "Pending" || i.Status == "Overdue"))
            .OrderByDescending(i => i.DueDateUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<SaasInvoice>> ListPaidInPeriodAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
    {
        return await dbContext.SaasInvoices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.Status == "Paid" && i.PaidAtUtc >= fromUtc && i.PaidAtUtc <= toUtc)
            .OrderByDescending(i => i.PaidAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SaasInvoice invoice, CancellationToken ct = default)
    {
        await dbContext.SaasInvoices.AddAsync(invoice, ct);
    }

    public void Update(SaasInvoice invoice)
    {
        dbContext.SaasInvoices.Update(invoice);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
