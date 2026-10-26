using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class WhatsAppTransientDataPurgeApplicationService(
    IOutboundWhatsAppMessageRepository messageRepository,
    IChatbotSessionRepository chatbotSessionRepository,
    ILogger<WhatsAppTransientDataPurgeApplicationService>? logger = null)
{
    public const int DefaultRetentionDays = 60;

    public async Task<Result<(int MediaMessagesPurged, int ChatbotSessionsPurged)>> PurgeTransientDataAsync(
        int retentionDays = DefaultRetentionDays,
        CancellationToken ct = default)
    {
        if (retentionDays < 1)
        {
            return Result<(int, int)>.Failure(new Error("whatsapp.retention_days.invalid", "O prazo de retenção deve ser de pelo menos 1 dia.", ErrorType.Validation));
        }

        var now = DateTimeOffset.UtcNow;
        var cutoffUtc = now.AddDays(-retentionDays);

        int totalMediaMessagesPurged = 0;

        // 1. Higienizar mídias base64 transitórias de mensagens antigas
        while (!ct.IsCancellationRequested)
        {
            var batch = await messageRepository.GetEligibleForMediaPurgeAsync(cutoffUtc, batchSize: 100, ct);
            if (batch.Count == 0) break;

            foreach (var message in batch)
            {
                message.PurgeTransientMedia(now);
            }

            await messageRepository.SaveChangesAsync(ct);
            totalMediaMessagesPurged += batch.Count;

            if (batch.Count < 100) break;
        }

        // 2. Remover sessões inativas ou concluídas de chatbot > 60 dias
        var chatbotSessionsPurged = await chatbotSessionRepository.PurgeInactiveSessionsOlderThanAsync(cutoffUtc, ct);

        logger?.LogInformation(
            "Higienização de dados transitórios de WhatsApp (LGPD Art. 16): {MediaCount} mídias de mensagens e {SessionCount} sessões de chatbot descartadas (> {Days} dias).",
            totalMediaMessagesPurged, chatbotSessionsPurged, retentionDays);

        return Result<(int, int)>.Success((totalMediaMessagesPurged, chatbotSessionsPurged));
    }
}
