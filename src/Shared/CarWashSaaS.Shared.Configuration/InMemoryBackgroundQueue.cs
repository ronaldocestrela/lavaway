using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Shared.Configuration;

public sealed class InMemoryBackgroundQueue : IBackgroundQueue
{
    private readonly Queue<TenantQueueMessage> _queue = new();

    public ValueTask EnqueueAsync(TenantQueueMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        lock (_queue)
        {
            _queue.Enqueue(message);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<TenantQueueMessage?> DequeueAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<TenantQueueMessage?>(cancellationToken);
        }

        lock (_queue)
        {
            if (_queue.Count == 0)
            {
                return ValueTask.FromResult<TenantQueueMessage?>(null);
            }

            return ValueTask.FromResult<TenantQueueMessage?>(_queue.Dequeue());
        }
    }
}
