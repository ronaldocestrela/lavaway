using CarWashSaaS.Api.Services;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace CarWashSaaS.IntegrationTests;

[Collection(RabbitMqQueueFixture.CollectionName)]
public sealed class RabbitMqBackgroundQueueIntegrationTests(RabbitMqQueueFixture fixture)
{
    [Fact]
    public async Task Queue_ShouldPersistAndRedeliverMessageUntilAcknowledged()
    {
        var queueName = $"lavaway-test-{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:RabbitMq:Queue"] = queueName,
                ["Messaging:RabbitMq:MaxDeliveryAttempts"] = "3"
            })
            .Build();
        var connectionFactory = new ConnectionFactory { Uri = new Uri(fixture.ConnectionString) };
        await using var queue = new RabbitMqBackgroundQueue(connectionFactory, configuration);
        var message = new TenantQueueMessage(Guid.NewGuid(), "test.persist", "payload");

        await queue.EnqueueAsync(message);

        await using (var firstDelivery = await queue.DequeueAsync(CancellationToken.None))
        {
            Assert.NotNull(firstDelivery);
            Assert.Equal(message.MessageId, firstDelivery!.Message.MessageId);
            Assert.Equal(message.TenantId, firstDelivery.Message.TenantId);
            Assert.Equal("test.persist", firstDelivery.Message.EventType);
            await firstDelivery.RetryAsync();
        }

        await using (var retryDelivery = await queue.DequeueAsync(CancellationToken.None))
        {
            Assert.NotNull(retryDelivery);
            Assert.Equal(message.MessageId, retryDelivery!.Message.MessageId);
            Assert.True(retryDelivery.DeliveryCount > 0);
            await retryDelivery.CompleteAsync();
        }

        Assert.Null(await queue.DequeueAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Queue_ShouldDeadLetterMessageAfterDeliveryLimit()
    {
        var queueName = $"lavaway-test-{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:RabbitMq:Queue"] = queueName,
                ["Messaging:RabbitMq:MaxDeliveryAttempts"] = "1"
            })
            .Build();
        var connectionFactory = new ConnectionFactory { Uri = new Uri(fixture.ConnectionString) };
        await using var queue = new RabbitMqBackgroundQueue(connectionFactory, configuration);
        var message = new TenantQueueMessage(Guid.NewGuid(), "test.dead-letter", "payload");

        await queue.EnqueueAsync(message);
        await using (var delivery = await queue.DequeueAsync(CancellationToken.None))
        {
            Assert.NotNull(delivery);
            await delivery!.RetryAsync();
        }

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        BasicGetResult? deadLetter = null;
        for (var i = 0; i < 20 && deadLetter is null; i++)
        {
            deadLetter = await channel.BasicGetAsync($"{queueName}.dead", false);
            if (deadLetter is null)
            {
                await Task.Delay(100);
            }
        }

        Assert.NotNull(deadLetter);
        Assert.Equal(message.MessageId.ToString("D"), deadLetter!.BasicProperties.MessageId);
        await channel.BasicAckAsync(deadLetter.DeliveryTag, false);
    }
}

public sealed class RabbitMqQueueFixture : IAsyncLifetime
{
    public const string CollectionName = "RabbitMQ durable queue";

    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("lavaway_test")
        .WithPassword("Lavaway_Test_only_123!")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(RabbitMqQueueFixture.CollectionName)]
public sealed class RabbitMqQueueCollection : ICollectionFixture<RabbitMqQueueFixture>;
