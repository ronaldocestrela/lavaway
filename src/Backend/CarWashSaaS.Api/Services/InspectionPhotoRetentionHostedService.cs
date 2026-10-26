using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Api.Services;

public sealed class InspectionPhotoRetentionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<InspectionPhotoRetentionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InspectionPhotoRetentionHostedService started.");

        // Atraso inicial de 30 segundos após startup para estabilização dos serviços
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRetentionPurgeScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred during periodic inspection photo retention scan.");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }

        logger.LogInformation("InspectionPhotoRetentionHostedService stopping.");
    }

    private async Task RunRetentionPurgeScanAsync(CancellationToken stoppingToken)
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

        foreach (var tenantId in tenantIds)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await using var tenantScope = scopeFactory.CreateAsyncScope();
                var tenantAccessor = tenantScope.ServiceProvider.GetRequiredService<CurrentTenantAccessor>();
                tenantAccessor.SetTenant(tenantId);

                var retentionService = tenantScope.ServiceProvider.GetRequiredService<InspectionPhotoRetentionApplicationService>();
                var result = await retentionService.PurgeExpiredInspectionPhotosAsync(tenantId, stoppingToken);

                if (result.IsSuccess && result.Value > 0)
                {
                    logger.LogInformation("Expurgo automatizado de vistoria (LGPD L3.1): {Count} foto(s) descartada(s) para o tenant {TenantId}.", result.Value, tenantId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to run inspection photo retention scan for tenant {TenantId}.", tenantId);
            }
        }
    }
}
