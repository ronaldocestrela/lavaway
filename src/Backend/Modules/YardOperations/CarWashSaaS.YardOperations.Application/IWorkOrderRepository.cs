using CarWashSaaS.YardOperations.Domain;

namespace CarWashSaaS.YardOperations.Application;

public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyCollection<WorkOrder>> ListRecentAsync(Guid tenantId, int limit = 20, CancellationToken ct = default);
    Task<IReadOnlyCollection<WorkOrder>> ListActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyCollection<WorkOrder>> GetWorkOrdersPendingSurveyAsync(Guid tenantId, DateTimeOffset cutoff, CancellationToken ct = default);
    Task<WorkOrder?> GetLatestCompletedOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default);
    Task<WorkOrder?> GetActiveOrderByPhoneAsync(Guid tenantId, string customerPhone, CancellationToken ct = default);
    Task<IReadOnlyCollection<WorkOrder>> ListSurveysAsync(Guid tenantId, int limit = 50, CancellationToken ct = default);
    Task AddAsync(WorkOrder workOrder, CancellationToken ct = default);
    void Update(WorkOrder workOrder);
}
