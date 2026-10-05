using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Api.Services;

public sealed class ReactivationCampaignHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReactivationCampaignHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ReactivationCampaignHostedService started.");

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCampaignScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred during periodic reactivation campaign scan.");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }

        logger.LogInformation("ReactivationCampaignHostedService stopping.");
    }

    private async Task RunCampaignScanAsync(CancellationToken stoppingToken)
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

                var campaignService = tenantScope.ServiceProvider.GetRequiredService<ReactivationCampaignApplicationService>();
                var result = await campaignService.ScanAndDispatchCampaignsAsync(tenantId, stoppingToken);

                if (result.IsSuccess && result.Value!.TotalDispatched > 0)
                {
                    logger.LogInformation("Dispatched {Count} reactivation campaign messages for tenant {TenantId}.", result.Value.TotalDispatched, tenantId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to run campaign scan for tenant {TenantId}.", tenantId);
            }
        }
    }
}
