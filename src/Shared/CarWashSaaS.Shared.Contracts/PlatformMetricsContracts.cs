namespace CarWashSaaS.Shared.Contracts;

public sealed record PlatformMetricsOverviewDto(
    PlatformSaasRevenueMetricsDto SaasRevenue,
    PlatformYardOperationalMetricsDto YardOperations,
    PlatformPixTransactionalMetricsDto PixTransactions,
    PlatformWhatsAppMetricsSummaryDto WhatsAppSummary,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc);

public sealed record PlatformSaasRevenueMetricsDto(
    decimal Mrr,
    decimal Arr,
    decimal ChurnRatePercentage,
    decimal Ltv,
    int ActiveSubscriptionsCount,
    int TrialSubscriptionsCount,
    int DelinquentSubscriptionsCount,
    int CanceledInPeriodCount,
    IReadOnlyList<SaasPlanDistributionDto> PlanDistribution);

public sealed record SaasPlanDistributionDto(
    string PlanTier,
    string PlanName,
    int SubscriptionsCount,
    decimal TotalMonthlyContribution);

public sealed record PlatformYardOperationalMetricsDto(
    int TotalAttendedVehicles,
    int InProgressWorkOrders,
    decimal AverageDailyAttendedVehicles,
    IReadOnlyList<DailyVehicleVolumeDto> DailyVolume);

public sealed record DailyVehicleVolumeDto(
    DateOnly Date,
    int VehiclesAttendedCount);

public sealed record PlatformPixTransactionalMetricsDto(
    decimal TotalAmountTransacted,
    int TotalTransactionsCount,
    decimal AverageTicket,
    IReadOnlyList<DailyPixVolumeDto> DailyVolume);

public sealed record DailyPixVolumeDto(
    DateOnly Date,
    decimal TotalAmount,
    int TransactionsCount);

public sealed record PlatformWhatsAppMetricsSummaryDto(
    int TotalConfiguredInstances,
    int OnlineInstancesCount,
    int OfflineInstancesCount,
    int TotalMessagesDispatchedInPeriod,
    int FailedMessagesInPeriod,
    decimal DeliverySuccessRatePercentage);

public sealed record PlatformWebhookLogDto(
    Guid Id,
    string Provider,
    string EventType,
    Guid? TenantId,
    string? TenantName,
    string Status,
    string? TxId,
    string? PayloadHash,
    string? Notes,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? ProcessedAtUtc);

public sealed record PlatformWhatsAppFailureDto(
    Guid MessageId,
    Guid TenantId,
    string TenantName,
    string RecipientPhoneMasked,
    string BodyPreview,
    string FailureReason,
    int AttemptCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastAttemptAtUtc);

public sealed record GetPlatformMetricsOverviewRequest(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    Guid? TenantId = null);

public sealed record GetPlatformWebhookLogsRequest(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Provider = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20);

public sealed record GetPlatformWhatsAppFailuresRequest(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    Guid? TenantId = null,
    int Page = 1,
    int PageSize = 20);

public interface IPlatformBillingMetricsLookup
{
    Task<PlatformSaasRevenueMetricsDto> GetRevenueMetricsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);
    Task<PlatformPixTransactionalMetricsDto> GetPixMetricsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, CancellationToken ct = default);
    Task<IReadOnlyList<PlatformWebhookLogDto>> ListWebhookLogsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, string? provider = null, string? status = null, int limit = 100, CancellationToken ct = default);
}

public interface IPlatformYardMetricsLookup
{
    Task<PlatformYardOperationalMetricsDto> GetYardMetricsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, CancellationToken ct = default);
}

public interface IPlatformWhatsAppObservabilityLookup
{
    Task<PlatformWhatsAppMetricsSummaryDto> GetWhatsAppMetricsSummaryAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<PlatformWhatsAppFailureDto>> ListFailedDispatchesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? tenantId = null, int limit = 50, CancellationToken ct = default);
}
