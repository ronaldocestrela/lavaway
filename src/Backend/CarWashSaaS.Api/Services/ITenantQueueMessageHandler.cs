using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Api.Services;

public interface ITenantQueueMessageHandler
{
    string EventType { get; }

    Task HandleAsync(TenantQueueMessage message, CancellationToken cancellationToken);
}