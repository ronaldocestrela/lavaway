using CarWashSaaS.Billing.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Application;

public sealed class PlatformBillingMetricsService(
    ITenantSaasSubscriptionRepository subscriptionRepository,
    ISaasInvoiceRepository invoiceRepository,
    IPixChargeRepository pixChargeRepository,
    IProcessedPaymentWebhookRepository paymentWebhookRepository,
    IProcessedSaasWebhookEventRepository saasWebhookRepository,
    IGlobalTenantLookup globalTenantLookup) : IPlatformBillingMetricsLookup
{
    public async Task<PlatformSaasRevenueMetricsDto> GetRevenueMetricsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
    {
        var subscriptions = await subscriptionRepository.ListAllAsync(ct);

        var activeSubs = subscriptions.Where(s =>
            s.Status == TenantSubscriptionStatus.Active ||
            s.Status == TenantSubscriptionStatus.GracePeriod).ToList();

        var mrr = activeSubs.Sum(s => s.MonthlyPrice);
        var arr = mrr * 12m;

        var activeCount = activeSubs.Count;
        var trialCount = subscriptions.Count(s => s.Status == TenantSubscriptionStatus.Trial);
        var delinquentCount = subscriptions.Count(s => s.Status == TenantSubscriptionStatus.Delinquent);

        var canceledInPeriod = subscriptions.Count(s =>
            s.Status == TenantSubscriptionStatus.Canceled &&
            s.UpdatedAtUtc >= fromUtc && s.UpdatedAtUtc <= toUtc);

        var churnDenominator = activeCount + canceledInPeriod;
        var churnRate = churnDenominator > 0
            ? Math.Round(((decimal)canceledInPeriod / churnDenominator) * 100m, 2)
            : 0m;

        var arpu = activeCount > 0 ? mrr / activeCount : 0m;
        decimal ltv;
        if (churnRate > 0)
        {
            ltv = Math.Round(arpu / (churnRate / 100m), 2);
        }
        else
        {
            var paidInvoices = await invoiceRepository.ListPaidInPeriodAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, ct);
            var distinctTenants = Math.Max(1, subscriptions.Select(s => s.TenantId).Distinct().Count());
            ltv = paidInvoices.Count > 0
                ? Math.Round(paidInvoices.Sum(i => i.Amount) / distinctTenants, 2)
                : Math.Round(arpu * 12m, 2);
        }

        var planDistribution = activeSubs
            .GroupBy(s => s.PlanTier)
            .OrderBy(g => g.Key)
            .Select(g => new SaasPlanDistributionDto(
                PlanTier: g.Key.ToString(),
                PlanName: ToPlanDisplayName(g.Key),
                SubscriptionsCount: g.Count(),
                TotalMonthlyContribution: g.Sum(s => s.MonthlyPrice)))
            .ToList();

        return new PlatformSaasRevenueMetricsDto(
            Mrr: mrr,
            Arr: arr,
            ChurnRatePercentage: churnRate,
            Ltv: ltv,
            ActiveSubscriptionsCount: activeCount,
            TrialSubscriptionsCount: trialCount,
            DelinquentSubscriptionsCount: delinquentCount,
            CanceledInPeriodCount: canceledInPeriod,
            PlanDistribution: planDistribution);
    }

    public async Task<PlatformPixTransactionalMetricsDto> GetPixMetricsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, CancellationToken ct = default)
    {
        var paidCharges = await pixChargeRepository.ListPaidInPeriodAsync(fromUtc, toUtc, tenantId, ct);

        var totalAmount = paidCharges.Sum(c => c.Amount);
        var totalCount = paidCharges.Count;
        var averageTicket = totalCount > 0 ? Math.Round(totalAmount / totalCount, 2) : 0m;

        var dailyVolume = paidCharges
            .Where(c => c.PaidAtUtc.HasValue)
            .GroupBy(c => DateOnly.FromDateTime(c.PaidAtUtc!.Value.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyPixVolumeDto(
                Date: g.Key,
                TotalAmount: g.Sum(c => c.Amount),
                TransactionsCount: g.Count()))
            .ToList();

        return new PlatformPixTransactionalMetricsDto(
            TotalAmountTransacted: totalAmount,
            TotalTransactionsCount: totalCount,
            AverageTicket: averageTicket,
            DailyVolume: dailyVolume);
    }

    public async Task<IReadOnlyList<PlatformWebhookLogDto>> ListWebhookLogsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, string? provider = null, string? status = null, int limit = 100, CancellationToken ct = default)
    {
        var paymentWebhooks = await paymentWebhookRepository.ListInPeriodAsync(fromUtc, toUtc, ct);
        var saasWebhooks = await saasWebhookRepository.ListInPeriodAsync(fromUtc, toUtc, ct);

        var logs = new List<PlatformWebhookLogDto>();

        foreach (var sw in saasWebhooks)
        {
            logs.Add(new PlatformWebhookLogDto(
                Id: sw.Id,
                Provider: "SaaS Billing (Gateway)",
                EventType: sw.EventType,
                TenantId: sw.TenantId,
                TenantName: null,
                Status: "Processed",
                TxId: null,
                PayloadHash: null,
                Notes: null,
                ReceivedAtUtc: sw.ReceivedAtUtc,
                ProcessedAtUtc: sw.ReceivedAtUtc));
        }

        foreach (var pw in paymentWebhooks)
        {
            logs.Add(new PlatformWebhookLogDto(
                Id: pw.Id,
                Provider: $"Pix ({pw.Provider})",
                EventType: "payment.received",
                TenantId: pw.TenantId,
                TenantName: null,
                Status: pw.Status,
                TxId: pw.TxId,
                PayloadHash: pw.PayloadHash,
                Notes: pw.Notes,
                ReceivedAtUtc: pw.ReceivedAtUtc,
                ProcessedAtUtc: pw.ProcessedAtUtc));
        }

        var filtered = logs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(provider))
        {
            filtered = filtered.Where(l => l.Provider.Contains(provider, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(l => l.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderByDescending(l => l.ReceivedAtUtc)
            .Take(limit)
            .ToList();

        var tenantNamesCache = new Dictionary<Guid, string>();
        var finalLogs = new List<PlatformWebhookLogDto>(ordered.Count);

        foreach (var item in ordered)
        {
            string? resolvedName = null;
            if (item.TenantId.HasValue && item.TenantId.Value != Guid.Empty)
            {
                if (!tenantNamesCache.TryGetValue(item.TenantId.Value, out resolvedName))
                {
                    var summaryResult = await globalTenantLookup.GetSummaryAsync(item.TenantId.Value, ct);
                    resolvedName = summaryResult.IsSuccess ? summaryResult.Value?.Name ?? "Tenant Desconhecido" : "Tenant Desconhecido";
                    tenantNamesCache[item.TenantId.Value] = resolvedName;
                }
            }

            finalLogs.Add(item with { TenantName = resolvedName });
        }

        return finalLogs;
    }

    private static string ToPlanDisplayName(SaasPlanTier tier) => tier switch
    {
        SaasPlanTier.Basic => "Básico",
        SaasPlanTier.Pro => "Pro",
        SaasPlanTier.Enterprise => "Enterprise",
        _ => tier.ToString()
    };
}
