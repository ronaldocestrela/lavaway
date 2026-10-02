namespace CarWashSaaS.Shared.Contracts;

public interface IBackgroundQueue
{
    ValueTask EnqueueAsync(TenantQueueMessage message, CancellationToken cancellationToken = default);

    ValueTask<IBackgroundQueueDelivery?> DequeueAsync(CancellationToken cancellationToken);
}

public interface IBackgroundQueueDelivery : IAsyncDisposable
{
    TenantQueueMessage Message { get; }
    int DeliveryCount { get; }

    Task CompleteAsync(CancellationToken cancellationToken = default);
    Task RetryAsync(CancellationToken cancellationToken = default);
    Task RejectAsync(CancellationToken cancellationToken = default);
}
