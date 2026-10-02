namespace CarWashSaaS.Shared.Contracts;

public interface ITenantQueueMessageHandler
{
    string EventType { get; }

    Task HandleAsync(TenantQueueMessage message, CancellationToken cancellationToken);
}
