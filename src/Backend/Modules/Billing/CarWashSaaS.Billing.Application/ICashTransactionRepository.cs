using CarWashSaaS.Billing.Domain;

namespace CarWashSaaS.Billing.Application;

public interface ICashTransactionRepository
{
    Task AddAsync(CashTransaction transaction, CancellationToken ct = default);
    Task<IReadOnlyList<CashTransaction>> ListByDateAsync(Guid tenantId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<CashTransaction>> ListByPeriodAsync(Guid tenantId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default);
    Task<bool> ExistsForWorkOrderAsync(Guid tenantId, Guid workOrderId, CashTransactionType type, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
