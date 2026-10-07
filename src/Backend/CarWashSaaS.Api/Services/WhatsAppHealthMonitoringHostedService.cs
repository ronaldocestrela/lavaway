using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.Api.Services;

public sealed class WhatsAppHealthMonitoringHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppHealthMonitoringHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WhatsApp Health Monitoring Hosted Service started.");

        using var timer = new PeriodicTimer(CheckInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunHealthCheckPassAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in WhatsApp Health Monitoring loop.");
            }
        }

        logger.LogInformation("WhatsApp Health Monitoring Hosted Service stopped.");
    }

    public async Task RunHealthCheckPassAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWhatsAppConnectionRepository>();
        var connectionService = scope.ServiceProvider.GetRequiredService<WhatsAppConnectionApplicationService>();
        var healthProvider = scope.ServiceProvider.GetService<IWhatsAppHealthCheckProvider>();

        if (healthProvider is null)
        {
            return;
        }

        var connections = await repository.ListAllConnectionsAsync(ct);
        var connected = connections.Where(c => c.Status == WhatsAppConnectionStatus.Connected).ToList();

        foreach (var connection in connected)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var healthResult = await healthProvider.CheckHealthAsync(connection.ProviderSessionId, ct);
                if (healthResult.IsSuccess)
                {
                    var health = healthResult.Value!;
                    if (!health.IsReachable ||
                        string.Equals(health.State, "close", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(health.State, "closed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(health.State, "disconnected", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning(
                            "[HEALTH MONITOR] Instância WhatsApp '{Session}' do tenant '{TenantId}' identificada como caída (estado: '{State}'). Sincronizando...",
                            connection.ProviderSessionId,
                            connection.TenantId,
                            health.State);

                        await connectionService.ApplyProviderStatusAsync(
                            connection.TenantId,
                            connection.ProviderSessionId,
                            "disconnected",
                            ct);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao verificar saúde da instância '{Session}'.", connection.ProviderSessionId);
            }
        }
    }
}
