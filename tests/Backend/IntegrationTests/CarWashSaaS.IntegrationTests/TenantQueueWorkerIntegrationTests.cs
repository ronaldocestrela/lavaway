using CarWashSaaS.Api.Services;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace CarWashSaaS.IntegrationTests;

[Collection(RabbitMqQueueFixture.CollectionName)]
public sealed class TenantQueueWorkerIntegrationTests(RabbitMqQueueFixture fixture)
{
    [Fact]
    public async Task Worker_ShouldProcessMessage_WithTenantScope_AndInvokeRegisteredHandler()
    {
        var queueName = $"lavaway-worker-test-{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:RabbitMq:Queue"] = queueName,
                ["Messaging:RabbitMq:MaxDeliveryAttempts"] = "3",
                ["Messaging:RabbitMq:RetryBaseDelayMilliseconds"] = "50",
                ["Messaging:RabbitMq:RetryMaxDelayMilliseconds"] = "100"
            })
            .Build();

        var connectionFactory = new ConnectionFactory { Uri = new Uri(fixture.ConnectionString) };
        var queue = new RabbitMqBackgroundQueue(connectionFactory, configuration);

        var services = new ServiceCollection();
        services.AddScoped<CurrentTenantAccessor>();
        services.AddScoped<ICurrentTenantAccessor>(sp => sp.GetRequiredService<CurrentTenantAccessor>());
        services.AddSingleton<IBackgroundQueue>(queue);
        services.AddSingleton<ILogger<TenantBrandingAuditQueueHandler>>(_ => NullLogger<TenantBrandingAuditQueueHandler>.Instance);
        services.AddScoped<ITenantQueueMessageHandler, TenantBrandingAuditQueueHandler>();

        var testObserver = new TestMessageObserver();
        services.AddSingleton(testObserver);
        services.AddScoped<ITenantQueueMessageHandler>(sp =>
            new TestCapturingHandler("test.captured.event", sp.GetRequiredService<ICurrentTenantAccessor>(), testObserver));

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var workerLogger = NullLogger<TenantQueueWorker>.Instance;
        var worker = new TenantQueueWorker(queue, scopeFactory, workerLogger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var workerTask = worker.StartAsync(cts.Token);

        var tenantId = Guid.NewGuid();
        var message = new TenantQueueMessage(tenantId, "test.captured.event", "payload-worker-test");

        await queue.EnqueueAsync(message, cts.Token);

        var handled = await testObserver.WaitForMessageAsync(TimeSpan.FromSeconds(10));
        Assert.True(handled, "Worker should have dispatched message to registered handler within timeout.");
        Assert.NotNull(testObserver.LastHandledMessage);
        Assert.Equal(message.MessageId, testObserver.LastHandledMessage!.MessageId);
        Assert.Equal(tenantId, testObserver.LastHandledMessage.TenantId);
        Assert.Equal(tenantId, testObserver.ObservedTenantInScope);

        await worker.StopAsync(CancellationToken.None);
        await workerTask;
        await queue.DisposeAsync();
    }

    [Fact]
    public async Task Worker_ShouldExecuteTenantBrandingAuditQueueHandler_EndToEnd()
    {
        var queueName = $"lavaway-branding-test-{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:RabbitMq:Queue"] = queueName,
                ["Messaging:RabbitMq:MaxDeliveryAttempts"] = "3"
            })
            .Build();

        var connectionFactory = new ConnectionFactory { Uri = new Uri(fixture.ConnectionString) };
        var queue = new RabbitMqBackgroundQueue(connectionFactory, configuration);

        var services = new ServiceCollection();
        services.AddScoped<CurrentTenantAccessor>();
        services.AddScoped<ICurrentTenantAccessor>(sp => sp.GetRequiredService<CurrentTenantAccessor>());
        services.AddSingleton<IBackgroundQueue>(queue);
        services.AddSingleton<ILogger<TenantBrandingAuditQueueHandler>>(_ => NullLogger<TenantBrandingAuditQueueHandler>.Instance);
        services.AddScoped<ITenantQueueMessageHandler, TenantBrandingAuditQueueHandler>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var worker = new TenantQueueWorker(queue, scopeFactory, NullLogger<TenantQueueWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var workerTask = worker.StartAsync(cts.Token);

        var tenantId = Guid.NewGuid();
        var message = new TenantQueueMessage(tenantId, TenantBrandingAuditQueueHandler.EventName, "/tenants/profile/logo/logo.png");

        await queue.EnqueueAsync(message, cts.Token);

        // Wait a moment for worker to process and acknowledge the message
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        // Verify the queue is now empty because message was acknowledged
        var nextDelivery = await queue.DequeueAsync(CancellationToken.None);
        Assert.Null(nextDelivery);

        await worker.StopAsync(CancellationToken.None);
        await workerTask;
        await queue.DisposeAsync();
    }

    private sealed class TestMessageObserver
    {
        private readonly TaskCompletionSource<bool> _tcs = new();

        public TenantQueueMessage? LastHandledMessage { get; private set; }
        public Guid? ObservedTenantInScope { get; private set; }

        public void Notify(TenantQueueMessage message, Guid? tenantInScope)
        {
            LastHandledMessage = message;
            ObservedTenantInScope = tenantInScope;
            _tcs.TrySetResult(true);
        }

        public async Task<bool> WaitForMessageAsync(TimeSpan timeout)
        {
            var delayTask = Task.Delay(timeout);
            var completed = await Task.WhenAny(_tcs.Task, delayTask);
            return completed == _tcs.Task && await _tcs.Task;
        }
    }

    private sealed class TestCapturingHandler(
        string eventType,
        ICurrentTenantAccessor currentTenantAccessor,
        TestMessageObserver observer) : ITenantQueueMessageHandler
    {
        public string EventType => eventType;

        public Task HandleAsync(TenantQueueMessage message, CancellationToken cancellationToken)
        {
            observer.Notify(message, currentTenantAccessor.TenantId);
            return Task.CompletedTask;
        }
    }
}
