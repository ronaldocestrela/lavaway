using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using Microsoft.EntityFrameworkCore;

namespace CarWashSaaS.Api.Services;

public sealed class AfterSalesSurveyHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AfterSalesSurveyHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AfterSalesSurveyHostedService started.");

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSurveyScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred during periodic after-sales survey scan.");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }

        logger.LogInformation("AfterSalesSurveyHostedService stopping.");
    }

    private async Task RunSurveyScanAsync(CancellationToken stoppingToken)
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

                var afterSalesService = tenantScope.ServiceProvider.GetRequiredService<AfterSalesApplicationService>();
                var result = await afterSalesService.ScanAndDispatchPendingSurveysAsync(tenantId, stoppingToken);

                if (result.IsSuccess && result.Value > 0)
                {
                    logger.LogInformation("Dispatched {Count} after-sales survey(s) for tenant {TenantId}.", result.Value, tenantId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to run survey scan for tenant {TenantId}.", tenantId);
            }
        }
    }
}
