using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Api.Services;

public sealed class PlatformObservabilityApplicationService(
    IPlatformBillingMetricsLookup billingMetricsLookup,
    IPlatformYardMetricsLookup yardMetricsLookup,
    IPlatformWhatsAppObservabilityLookup whatsAppObservabilityLookup)
{
    public async Task<Result<PlatformMetricsOverviewDto>> GetOverviewAsync(
        GetPlatformMetricsOverviewRequest request,
        CancellationToken ct = default)
    {
        var toUtc = request.ToUtc ?? DateTimeOffset.UtcNow;
        var fromUtc = request.FromUtc ?? toUtc.AddDays(-30);

        if (fromUtc > toUtc)
        {
            return Result<PlatformMetricsOverviewDto>.Failure(
                new Error("metrics.invalid_period", "A data inicial não pode ser maior que a data final.", ErrorType.Validation));
        }

        var saasRevenue = await billingMetricsLookup.GetRevenueMetricsAsync(fromUtc, toUtc, ct);
        var yardOperations = await yardMetricsLookup.GetYardMetricsAsync(fromUtc, toUtc, request.TenantId, ct);
        var pixTransactions = await billingMetricsLookup.GetPixMetricsAsync(fromUtc, toUtc, request.TenantId, ct);
        var whatsAppSummary = await whatsAppObservabilityLookup.GetWhatsAppMetricsSummaryAsync(fromUtc, toUtc, ct);

        var overview = new PlatformMetricsOverviewDto(
            SaasRevenue: saasRevenue,
            YardOperations: yardOperations,
            PixTransactions: pixTransactions,
            WhatsAppSummary: whatsAppSummary,
            PeriodStartUtc: fromUtc,
            PeriodEndUtc: toUtc);

        return Result<PlatformMetricsOverviewDto>.Success(overview);
    }

    public async Task<Result<IReadOnlyList<PlatformWebhookLogDto>>> GetWebhookLogsAsync(
        GetPlatformWebhookLogsRequest request,
        CancellationToken ct = default)
    {
        var toUtc = request.ToUtc ?? DateTimeOffset.UtcNow;
        var fromUtc = request.FromUtc ?? toUtc.AddDays(-30);

        var logs = await billingMetricsLookup.ListWebhookLogsAsync(
            fromUtc,
            toUtc,
            request.Provider,
            request.Status,
            limit: request.PageSize * request.Page,
            ct);

        var paginated = logs
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<IReadOnlyList<PlatformWebhookLogDto>>.Success(paginated);
    }

    public async Task<Result<IReadOnlyList<PlatformWhatsAppFailureDto>>> GetWhatsAppFailuresAsync(
        GetPlatformWhatsAppFailuresRequest request,
        CancellationToken ct = default)
    {
        var toUtc = request.ToUtc ?? DateTimeOffset.UtcNow;
        var fromUtc = request.FromUtc ?? toUtc.AddDays(-30);

        var failures = await whatsAppObservabilityLookup.ListFailedDispatchesAsync(
            fromUtc,
            toUtc,
            request.TenantId,
            limit: request.PageSize * request.Page,
            ct);

        var paginated = failures
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<IReadOnlyList<PlatformWhatsAppFailureDto>>.Success(paginated);
    }
}
