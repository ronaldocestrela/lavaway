using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.YardOperations.Application;

public interface IYardRealtimeNotifier
{
    Task NotifyWorkOrderMovedAsync(Guid tenantId, WorkOrderMovedNotification notification, CancellationToken ct = default);
    Task NotifyOperatorAssignedAsync(Guid tenantId, Guid workOrderId, Guid? operatorId, string? operatorName, CancellationToken ct = default);
}
