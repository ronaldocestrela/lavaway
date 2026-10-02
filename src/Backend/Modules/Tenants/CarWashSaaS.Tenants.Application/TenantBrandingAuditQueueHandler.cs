using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Tenants.Application;

public sealed class TenantBrandingAuditQueueHandler(
    ICurrentTenantAccessor currentTenantAccessor,
    ILogger<TenantBrandingAuditQueueHandler> logger) : ITenantQueueMessageHandler
{
    public const string EventName = "tenant.branding.updated";

    public string EventType => EventName;

    public Task HandleAsync(TenantQueueMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (currentTenantAccessor.TenantId != message.TenantId)
        {
            logger.LogError("Cross-tenant violation detected in background handler: scope tenant {ScopeTenantId} does not match message tenant {MessageTenantId}.",
                currentTenantAccessor.TenantId, message.TenantId);
            throw new InvalidOperationException("Tenant scope mismatch in queue handler.");
        }

        logger.LogInformation("Branding updated successfully for tenant {TenantId}. Logo path: {LogoPath}, MessageId: {MessageId}",
            message.TenantId, message.Payload, message.MessageId);

        return Task.CompletedTask;
    }
}
