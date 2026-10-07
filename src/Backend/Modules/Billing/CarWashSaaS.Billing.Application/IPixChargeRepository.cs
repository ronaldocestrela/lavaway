using CarWashSaaS.Billing.Domain;

namespace CarWashSaaS.Billing.Application;

public interface IPixChargeRepository
{
    Task<PixCharge?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<PixCharge?> GetActiveByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default);
    Task<PixCharge?> GetLatestByWorkOrderIdAsync(Guid tenantId, Guid workOrderId, CancellationToken ct = default);
    Task<PixCharge?> GetByTxIdAsync(Guid tenantId, string txId, CancellationToken ct = default);
    Task<IReadOnlyList<PixCharge>> ListPaidInPeriodAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PixCharge>>([]);
    Task AddAsync(PixCharge charge, CancellationToken ct = default);
    void Update(PixCharge charge);
    Task SaveChangesAsync(CancellationToken ct = default);
}
