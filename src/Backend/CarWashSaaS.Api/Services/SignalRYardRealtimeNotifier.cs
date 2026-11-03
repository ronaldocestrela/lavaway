using CarWashSaaS.Api.Hubs;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.YardOperations.Application;
using Microsoft.AspNetCore.SignalR;

namespace CarWashSaaS.Api.Services;

public sealed class SignalRYardRealtimeNotifier(IHubContext<YardHub> hubContext) : IYardRealtimeNotifier
{
    public async Task NotifyWorkOrderMovedAsync(Guid tenantId, WorkOrderMovedNotification notification, CancellationToken ct = default)
    {
        var groupName = YardHub.GetTenantGroupName(tenantId);
        await hubContext.Clients.Group(groupName).SendAsync("WorkOrderMoved", notification, ct);
    }

    public async Task NotifyOperatorAssignedAsync(
        Guid tenantId,
        Guid workOrderId,
        Guid? operatorId,
        string? operatorName,
        Guid? secondaryOperatorId = null,
        string? secondaryOperatorName = null,
        CancellationToken ct = default)
    {
        var groupName = YardHub.GetTenantGroupName(tenantId);
        await hubContext.Clients.Group(groupName).SendAsync("OperatorAssigned", new
        {
            WorkOrderId = workOrderId,
            OperatorId = operatorId,
            OperatorName = operatorName,
            SecondaryOperatorId = secondaryOperatorId,
            SecondaryOperatorName = secondaryOperatorName
        }, ct);
    }
}
