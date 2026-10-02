using System.Globalization;
using System.Text.Json;
using CarWashSaaS.Shared.Contracts;
using RabbitMQ.Client;

namespace CarWashSaaS.Api.Services;

public sealed class RabbitMqBackgroundQueue : IBackgroundQueue, IAsyncDisposable
{
    private readonly Lazy<Task<IConnection>> _connection;
    private readonly string _queueName;
    private readonly string _deadLetterExchange;
    private readonly string _deadLetterQueue;
    private readonly int _maxDeliveryAttempts;
    private readonly int _retryBaseDelayMilliseconds;
    private readonly int _retryMaxDelayMilliseconds;

    public RabbitMqBackgroundQueue(IConnectionFactory connectionFactory, IConfiguration configuration)
    {
        _connection = new Lazy<Task<IConnection>>(() => connectionFactory.CreateConnectionAsync());
        _queueName = configuration["Messaging:RabbitMq:Queue"] ?? "lavaway.tenant-events";
        _deadLetterExchange = $"{_queueName}.dead-letter";
        _deadLetterQueue = $"{_queueName}.dead";
        _maxDeliveryAttempts = Math.Max(1, configuration.GetValue("Messaging:RabbitMq:MaxDeliveryAttempts", 5));
        _retryBaseDelayMilliseconds = Math.Clamp(configuration.GetValue("Messaging:RabbitMq:RetryBaseDelayMilliseconds", 500), 1, 30000);
        _retryMaxDelayMilliseconds = Math.Clamp(configuration.GetValue("Messaging:RabbitMq:RetryMaxDelayMilliseconds", 30000), _retryBaseDelayMilliseconds, 30000);
    }

    public async ValueTask EnqueueAsync(TenantQueueMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await using var channel = await CreateChannelAsync(cancellationToken);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.MessageId.ToString("D")
        };

        await channel.BasicPublishAsync(string.Empty, _queueName, true, properties, body, cancellationToken);
    }

    public async ValueTask<IBackgroundQueueDelivery?> DequeueAsync(CancellationToken cancellationToken)
    {
        var channel = await CreateChannelAsync(cancellationToken);
        var delivery = await channel.BasicGetAsync(_queueName, false, cancellationToken);
        if (delivery is null)
        {
            await channel.DisposeAsync();
            return null;
        }

        TenantQueueMessage message;
        try
        {
            message = JsonSerializer.Deserialize<TenantQueueMessage>(delivery.Body.Span)
                ?? throw new JsonException("Queue message payload was empty.");
        }
        catch (JsonException)
        {
            await channel.BasicNackAsync(delivery.DeliveryTag, false, false, cancellationToken);
            await channel.DisposeAsync();
            throw;
        }

        return new RabbitMqDelivery(
            channel,
            delivery.DeliveryTag,
            message,
            GetDeliveryCount(delivery),
            _maxDeliveryAttempts,
            _retryBaseDelayMilliseconds,
            _retryMaxDelayMilliseconds);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            await (await _connection.Value).DisposeAsync();
        }
    }

    private async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken)
    {
        var channelOptions = new CreateChannelOptions(true, true);
        var channel = await (await _connection.Value).CreateChannelAsync(channelOptions, cancellationToken);
        try
        {
            await DeclareTopologyAsync(channel, cancellationToken);
            return channel;
        }
        catch
        {
            await channel.DisposeAsync();
            throw;
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(_deadLetterExchange, ExchangeType.Direct, true, false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(_deadLetterQueue, true, false, false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(_deadLetterQueue, _deadLetterExchange, "dead", cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-delivery-limit"] = _maxDeliveryAttempts,
            ["x-dead-letter-exchange"] = _deadLetterExchange,
            ["x-dead-letter-routing-key"] = "dead"
        };
        await channel.QueueDeclareAsync(_queueName, true, false, false, arguments, cancellationToken: cancellationToken);
    }

    private static int GetDeliveryCount(BasicGetResult delivery)
    {
        if (delivery.BasicProperties.Headers is { } headers &&
            headers.TryGetValue("x-delivery-count", out var count) &&
            int.TryParse(Convert.ToString(count, CultureInfo.InvariantCulture), out var parsedCount))
        {
            return parsedCount;
        }

        return delivery.Redelivered ? 1 : 0;
    }

    private sealed class RabbitMqDelivery(
        IChannel channel,
        ulong deliveryTag,
        TenantQueueMessage message,
        int deliveryCount,
        int maxDeliveryAttempts,
        int retryBaseDelayMilliseconds,
        int retryMaxDelayMilliseconds) : IBackgroundQueueDelivery
    {
        private int _settled;
        private int _disposed;

        public TenantQueueMessage Message => message;
        public int DeliveryCount => deliveryCount;

        public Task CompleteAsync(CancellationToken cancellationToken = default) =>
            SettleAsync(async () => await channel.BasicAckAsync(deliveryTag, false, cancellationToken));

        public Task RetryAsync(CancellationToken cancellationToken = default) => SettleAsync(async () =>
        {
            if (deliveryCount + 1 >= maxDeliveryAttempts)
            {
                await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
                return;
            }

            var multiplier = 1 << Math.Min(deliveryCount, 16);
            var delayMilliseconds = Math.Min(retryMaxDelayMilliseconds, retryBaseDelayMilliseconds * multiplier);
            await Task.Delay(delayMilliseconds, cancellationToken);
            await channel.BasicNackAsync(deliveryTag, false, true, cancellationToken);
        });

        public Task RejectAsync(CancellationToken cancellationToken = default) =>
            SettleAsync(async () => await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken));

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                await channel.DisposeAsync();
            }
        }

        private async Task SettleAsync(Func<Task> settle)
        {
            if (Interlocked.Exchange(ref _settled, 1) != 0)
            {
                return;
            }

            try
            {
                await settle();
            }
            finally
            {
                await DisposeAsync();
            }
        }
    }
}
