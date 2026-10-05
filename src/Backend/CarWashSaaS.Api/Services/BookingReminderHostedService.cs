using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Api.Services;

public sealed class BookingReminderHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<BookingReminderHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("BookingReminderHostedService started.");

        // Pequeno atraso na inicialização para permitir que o app suba
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunReminderScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred during periodic booking reminder scan.");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }

        logger.LogInformation("BookingReminderHostedService stopping.");
    }

    private async Task RunReminderScanAsync(CancellationToken stoppingToken)
    {
        List<Guid> tenantIds;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var tenantsContext = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
            tenantIds = await tenantsContext.Tenants
                .AsNoTracking()
                .Select(t => t.Id)
                .ToListAsync(stoppingToken);
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var tenantId in tenantIds)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await using var tenantScope = scopeFactory.CreateAsyncScope();
                var tenantAccessor = tenantScope.ServiceProvider.GetRequiredService<CurrentTenantAccessor>();
                tenantAccessor.SetTenant(tenantId);

                var reminderService = tenantScope.ServiceProvider.GetRequiredService<BookingReminderApplicationService>();
                await reminderService.ExecuteReminderScanForTenantAsync(tenantId, now, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to run reminder scan for tenant {TenantId}.", tenantId);
            }
        }
    }
}
