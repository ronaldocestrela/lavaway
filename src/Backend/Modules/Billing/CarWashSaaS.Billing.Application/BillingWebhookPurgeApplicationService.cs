using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Billing.Application;

public sealed class BillingWebhookPurgeApplicationService(
    IProcessedPaymentWebhookRepository paymentWebhookRepository,
    IProcessedSaasWebhookEventRepository saasWebhookRepository,
    ILogger<BillingWebhookPurgeApplicationService>? logger = null)
{
    public const int DefaultRetentionDays = 60;

    public async Task<Result<(int PaymentWebhooksPurged, int SaasWebhooksPurged)>> PurgeOldWebhookPayloadsAsync(
        int retentionDays = DefaultRetentionDays,
        CancellationToken ct = default)
    {
        if (retentionDays < 1)
        {
            return Result<(int, int)>.Failure(new Error("billing.retention_days.invalid", "O prazo de retenção deve ser de pelo menos 1 dia.", ErrorType.Validation));
        }

        var now = DateTimeOffset.UtcNow;
        var cutoffUtc = now.AddDays(-retentionDays);

        int totalPaymentWebhooksPurged = 0;
        int totalSaasWebhooksPurged = 0;

        // 1. Processar ProcessedPaymentWebhooks em lotes
        while (!ct.IsCancellationRequested)
        {
            var batch = await paymentWebhookRepository.GetEligibleForPayloadPurgeAsync(cutoffUtc, batchSize: 100, ct);
            if (batch.Count == 0) break;

            foreach (var webhook in batch)
            {
                webhook.PurgePayload(now);
            }

            await paymentWebhookRepository.SaveChangesAsync(ct);
            totalPaymentWebhooksPurged += batch.Count;

            if (batch.Count < 100) break;
        }

        // 2. Processar ProcessedSaasWebhookEvents em lotes
        while (!ct.IsCancellationRequested)
        {
            var batch = await saasWebhookRepository.GetEligibleForPayloadPurgeAsync(cutoffUtc, batchSize: 100, ct);
            if (batch.Count == 0) break;

            foreach (var saasWebhook in batch)
            {
                saasWebhook.PurgePayload(now);
            }

            await saasWebhookRepository.SaveChangesAsync(ct);
            totalSaasWebhooksPurged += batch.Count;

            if (batch.Count < 100) break;
        }

        logger?.LogInformation(
            "Higienização de webhooks (LGPD Art. 16): {PaymentCount} webhooks de pagamento e {SaasCount} webhooks de SaaS higienizados (> {Days} dias).",
            totalPaymentWebhooksPurged, totalSaasWebhooksPurged, retentionDays);

        return Result<(int, int)>.Success((totalPaymentWebhooksPurged, totalSaasWebhooksPurged));
    }
}
