using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Shared.Configuration;

public sealed class InMemoryBackgroundQueue : IBackgroundQueue
{
    private readonly Queue<QueueEntry> _queue = new();

    public ValueTask EnqueueAsync(TenantQueueMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        lock (_queue)
        {
            _queue.Enqueue(new QueueEntry(message, 0));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IBackgroundQueueDelivery?> DequeueAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<IBackgroundQueueDelivery?>(cancellationToken);
        }

        lock (_queue)
        {
            if (_queue.Count == 0)
            {
                return ValueTask.FromResult<IBackgroundQueueDelivery?>(null);
            }

            return ValueTask.FromResult<IBackgroundQueueDelivery?>(new InMemoryDelivery(this, _queue.Dequeue()));
        }
    }

    private void Requeue(QueueEntry entry)
    {
        lock (_queue)
        {
            _queue.Enqueue(entry with { DeliveryCount = entry.DeliveryCount + 1 });
        }
    }

    private sealed record QueueEntry(TenantQueueMessage Message, int DeliveryCount);

    private sealed class InMemoryDelivery(InMemoryBackgroundQueue queue, QueueEntry entry) : IBackgroundQueueDelivery
    {
        private int _settled;

        public TenantQueueMessage Message => entry.Message;
        public int DeliveryCount => entry.DeliveryCount;

        public Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Exchange(ref _settled, 1);
            return Task.CompletedTask;
        }

        public Task RetryAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Exchange(ref _settled, 1) == 0)
            {
                queue.Requeue(entry);
            }

            return Task.CompletedTask;
        }

        public Task RejectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Exchange(ref _settled, 1);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
