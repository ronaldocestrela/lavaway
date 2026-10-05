using System.Text.Json;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.YardOperations.Application;

public sealed class WorkOrderReadyNotificationQueueHandler(
    WorkOrderNotificationApplicationService notificationService,
    ILogger<WorkOrderReadyNotificationQueueHandler> logger) : ITenantQueueMessageHandler
{
    public string EventType => WorkOrderNotificationEvents.ReadyForPickup;

    public async Task HandleAsync(TenantQueueMessage message, CancellationToken cancellationToken)
    {
        Guid workOrderId = Guid.Empty;
        string? customMessage = null;

        try
        {
            var payload = JsonSerializer.Deserialize<WorkOrderReadyEventPayload>(message.Payload);
            if (payload is not null)
            {
                workOrderId = payload.WorkOrderId;
                customMessage = payload.CustomMessage;
            }
        }
        catch
        {
            if (!Guid.TryParse(message.Payload, out workOrderId))
            {
                workOrderId = message.MessageId;
            }
        }

        if (workOrderId == Guid.Empty)
        {
            logger.LogWarning("Work order ID not resolved for queue message {MessageId}", message.MessageId);
            return;
        }

        var result = await notificationService.SendReadyForPickupNotificationAsync(
            message.TenantId,
            workOrderId,
            customMessage,
            cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Ready for pickup notification failed for work order {WorkOrderId}: {Error}",
                workOrderId, result.Error?.Description);
        }
        else
        {
            logger.LogInformation("Ready for pickup notification queued/sent for work order {WorkOrderId}", workOrderId);
        }
    }
}

public sealed class WorkOrderReceiptNotificationQueueHandler(
    WorkOrderNotificationApplicationService notificationService,
    ILogger<WorkOrderReceiptNotificationQueueHandler> logger) : ITenantQueueMessageHandler
{
    public string EventType => WorkOrderNotificationEvents.ReceiptRequested;

    public async Task HandleAsync(TenantQueueMessage message, CancellationToken cancellationToken)
    {
        Guid workOrderId = Guid.Empty;
        string? customMessage = null;

        try
        {
            var payload = JsonSerializer.Deserialize<WorkOrderReceiptEventPayload>(message.Payload);
            if (payload is not null)
            {
                workOrderId = payload.WorkOrderId;
                customMessage = payload.CustomMessage;
            }
        }
        catch
        {
            if (!Guid.TryParse(message.Payload, out workOrderId))
            {
                workOrderId = message.MessageId;
            }
        }

        if (workOrderId == Guid.Empty)
        {
            logger.LogWarning("Work order ID not resolved for receipt queue message {MessageId}", message.MessageId);
            return;
        }

        var result = await notificationService.SendReceiptNotificationAsync(
            message.TenantId,
            workOrderId,
            customMessage,
            cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Receipt notification failed for work order {WorkOrderId}: {Error}",
                workOrderId, result.Error?.Description);
        }
        else
        {
            logger.LogInformation("Receipt notification queued/sent for work order {WorkOrderId}", workOrderId);
        }
    }
}
