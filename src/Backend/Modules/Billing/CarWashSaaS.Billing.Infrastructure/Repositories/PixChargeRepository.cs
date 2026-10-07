using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Billing.Infrastructure.Repositories;

public sealed class PixChargeRepository(BillingDbContext dbContext) : IPixChargeRepository
{
    public Task<PixCharge?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        dbContext.PixCharges.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public Task<PixCharge?> GetActiveByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
        dbContext.PixCharges
            .Where(c => c.TenantId == tenantId && c.WorkOrderId == workOrderId && c.Status == "Pending")
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<PixCharge?> GetLatestByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default) =>
        dbContext.PixCharges
            .Where(c => c.TenantId == tenantId && c.WorkOrderId == workOrderId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<PixCharge?> GetByTxIdAsync(Guid tenantId, string txId, CancellationToken ct = default) =>
        dbContext.PixCharges
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.TxId == txId, ct);

    public async Task<IReadOnlyList<PixCharge>> ListPaidInPeriodAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, CancellationToken ct = default)
    {
        var query = dbContext.PixCharges
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Status == PixChargeStatusConstants.Paid && c.PaidAtUtc >= fromUtc && c.PaidAtUtc <= toUtc);

        if (tenantId.HasValue)
        {
            query = query.Where(c => c.TenantId == tenantId.Value);
        }

        return await query.OrderByDescending(c => c.PaidAtUtc).ToListAsync(ct);
    }

    public async Task AddAsync(PixCharge charge, CancellationToken ct = default) =>
        await dbContext.PixCharges.AddAsync(charge, ct);

    public void Update(PixCharge charge) =>
        dbContext.PixCharges.Update(charge);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);
}
