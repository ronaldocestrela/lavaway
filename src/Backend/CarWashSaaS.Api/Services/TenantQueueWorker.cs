using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Shared.Configuration;

namespace CarWashSaaS.Api.Services;

public sealed class TenantQueueWorker(
    IBackgroundQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<TenantQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delivery = await queue.DequeueAsync(stoppingToken);
            if (delivery is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                continue;
            }

            await using (delivery)
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<CurrentTenantAccessor>();
                tenantAccessor.SetTenant(delivery.Message.TenantId);
                var handler = scope.ServiceProvider.GetServices<ITenantQueueMessageHandler>()
                    .SingleOrDefault(candidate => string.Equals(candidate.EventType, delivery.Message.EventType, StringComparison.Ordinal));

                if (handler is null)
                {
                    logger.LogWarning("No background handler is registered for event {EventType}.", delivery.Message.EventType);
                    await delivery.RejectAsync(stoppingToken);
                    continue;
                }

                try
                {
                    await handler.HandleAsync(delivery.Message, stoppingToken);
                    await delivery.CompleteAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Background event {EventType} failed for tenant {TenantId}.",
                        delivery.Message.EventType, delivery.Message.TenantId);
                    await delivery.RetryAsync(stoppingToken);
                }
            }
        }
    }
}