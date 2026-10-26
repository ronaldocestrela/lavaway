using CarWashSaaS.Billing.Application;
using CarWashSaaS.WhatsApp.Application;

namespace CarWashSaaS.Api.Services;

public sealed class TransientDataRetentionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<TransientDataRetentionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TransientDataRetentionHostedService (LGPD L3.2) started.");

        // Atraso inicial de 45 segundos após startup para estabilização dos serviços
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunTransientDataPurgeScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred during periodic transient data and webhook retention scan.");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }

        logger.LogInformation("TransientDataRetentionHostedService (LGPD L3.2) stopping.");
    }

    private async Task RunTransientDataPurgeScanAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        // 1. Higienização de Webhooks de Billing (Pagar.me, MercadoPago e Stripe SaaS)
        var billingPurgeService = scope.ServiceProvider.GetRequiredService<BillingWebhookPurgeApplicationService>();
        var billingResult = await billingPurgeService.PurgeOldWebhookPayloadsAsync(retentionDays: 60, stoppingToken);

        if (billingResult.IsSuccess)
        {
            var (paymentCount, saasCount) = billingResult.Value;
            if (paymentCount > 0 || saasCount > 0)
            {
                logger.LogInformation(
                    "Expurgo LGPD L3.2 concluído: {PaymentCount} webhooks de pagamento e {SaasCount} webhooks SaaS higienizados (> 60 dias).",
                    paymentCount, saasCount);
            }
        }
        else
        {
            logger.LogWarning("Falha ao executar expurgo de webhooks de billing: {Error}", billingResult.Error?.Description);
        }

        // 2. Higienização de Mídias e Sessões Transitórias do WhatsApp
        var whatsAppPurgeService = scope.ServiceProvider.GetRequiredService<WhatsAppTransientDataPurgeApplicationService>();
        var whatsAppResult = await whatsAppPurgeService.PurgeTransientDataAsync(retentionDays: 60, stoppingToken);

        if (whatsAppResult.IsSuccess)
        {
            var (mediaCount, sessionsCount) = whatsAppResult.Value;
            if (mediaCount > 0 || sessionsCount > 0)
            {
                logger.LogInformation(
                    "Expurgo LGPD L3.2 WhatsApp concluído: {MediaCount} mídias e {SessionsCount} sessões de chatbot descartadas (> 60 dias).",
                    mediaCount, sessionsCount);
            }
        }
        else
        {
            logger.LogWarning("Falha ao executar expurgo de dados transitórios de WhatsApp: {Error}", whatsAppResult.Error?.Description);
        }
    }
}
