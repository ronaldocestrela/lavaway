namespace CarWashSaaS.Shared.Contracts;

public interface IBackgroundQueue
{
    ValueTask EnqueueAsync(TenantQueueMessage message, CancellationToken cancellationToken = default);

    ValueTask<TenantQueueMessage?> DequeueAsync(CancellationToken cancellationToken);
}
