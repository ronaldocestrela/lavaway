using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.WhatsApp.Domain;

namespace CarWashSaaS.WhatsApp.Application;

public sealed class PlatformWhatsAppObservabilityService(
    IWhatsAppConnectionRepository connectionRepository,
    IOutboundWhatsAppMessageRepository messageRepository,
    IGlobalTenantLookup globalTenantLookup) : IPlatformWhatsAppObservabilityLookup
{
    public async Task<PlatformWhatsAppMetricsSummaryDto> GetWhatsAppMetricsSummaryAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
    {
        var connections = await connectionRepository.ListAllConnectionsAsync(ct);

        var totalInstances = connections.Count;
        var onlineCount = connections.Count(c => c.Status == WhatsAppConnectionStatus.Connected);
        var offlineCount = totalInstances - onlineCount;

        var messages = await messageRepository.ListForPlatformMetricsAsync(fromUtc, toUtc, null, ct);
        var totalMessages = messages.Count;
        var failedMessages = messages.Count(m => m.Status == WhatsAppMessageStatus.Failed);

        var successRate = totalMessages > 0
            ? Math.Round(((decimal)(totalMessages - failedMessages) / totalMessages) * 100m, 2)
            : 100m;

        return new PlatformWhatsAppMetricsSummaryDto(
            TotalConfiguredInstances: totalInstances,
            OnlineInstancesCount: onlineCount,
            OfflineInstancesCount: offlineCount,
            TotalMessagesDispatchedInPeriod: totalMessages,
            FailedMessagesInPeriod: failedMessages,
            DeliverySuccessRatePercentage: successRate);
    }

    public async Task<IReadOnlyList<PlatformWhatsAppFailureDto>> ListFailedDispatchesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? tenantId = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var messages = await messageRepository.ListForPlatformMetricsAsync(fromUtc, toUtc, tenantId, ct);

        var failedMessages = messages
            .Where(m => m.Status == WhatsAppMessageStatus.Failed)
            .OrderByDescending(m => m.UpdatedAt)
            .Take(limit)
            .ToList();

        var tenantNamesCache = new Dictionary<Guid, string>();
        var result = new List<PlatformWhatsAppFailureDto>(failedMessages.Count);

        foreach (var msg in failedMessages)
        {
            if (!tenantNamesCache.TryGetValue(msg.TenantId, out var tenantName))
            {
                var summaryResult = await globalTenantLookup.GetSummaryAsync(msg.TenantId, ct);
                tenantName = summaryResult.IsSuccess ? summaryResult.Value?.Name ?? "Tenant Desconhecido" : "Tenant Desconhecido";
                tenantNamesCache[msg.TenantId] = tenantName;
            }

            var bodyPreview = msg.Body.Length > 80 ? msg.Body[..77] + "..." : msg.Body;
            var maskedPhone = MaskPhoneNumber(msg.RecipientPhone);

            result.Add(new PlatformWhatsAppFailureDto(
                MessageId: msg.Id,
                TenantId: msg.TenantId,
                TenantName: tenantName,
                RecipientPhoneMasked: maskedPhone,
                BodyPreview: bodyPreview,
                FailureReason: msg.FailureReason ?? "Erro desconhecido pelo provedor",
                AttemptCount: msg.AttemptCount,
                CreatedAtUtc: msg.CreatedAt,
                LastAttemptAtUtc: msg.UpdatedAt));
        }

        return result;
    }

    private static string MaskPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 8)
        {
            return "******";
        }

        var clean = phone.Trim();
        if (clean.Length <= 10)
        {
            return $"{clean[..2]} ****-{clean[^4..]}";
        }

        return $"{clean[..4]} ****-{clean[^4..]}";
    }
}
