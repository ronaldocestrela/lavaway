namespace CarWashSaaS.Shared.Contracts;

public sealed record SaasPlanDto(
    SaasPlanTier Tier,
    string Name,
    string Description,
    decimal MonthlyPrice,
    int MaxWorkOrdersPerCycle,
    int MaxWhatsAppMessagesPerCycle,
    bool HasCustomerSubscriptions,
    bool HasLoyalty,
    bool HasCommissions,
    bool HasAiChatbot,
    int MaxTeamMembers);

public sealed record TenantQuotaStatusDto(
    bool CanProceed,
    SaasPlanTier PlanTier,
    TenantSubscriptionStatus SubscriptionStatus,
    int UsedWorkOrders,
    int MaxWorkOrders,
    int UsedWhatsAppMessages,
    int MaxWhatsAppMessages,
    string? Reason = null);

public sealed record SaasInvoiceDto(
    Guid Id,
    Guid TenantId,
    string GatewayInvoiceId,
    decimal Amount,
    DateTimeOffset DueDateUtc,
    DateTimeOffset? PaidAtUtc,
    string Status,
    string? PaymentUrl,
    string? PixQrCode,
    string? PixCopiaECola);

public sealed record TenantSubscriptionOverviewDto(
    Guid TenantId,
    string TenantName,
    SaasPlanTier PlanTier,
    string PlanName,
    decimal MonthlyPrice,
    TenantSubscriptionStatus Status,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    DateTimeOffset? GracePeriodEndsAtUtc,
    DateTimeOffset? NextBillingDateUtc,
    int UsedWorkOrders,
    int MaxWorkOrders,
    int UsedWhatsAppMessages,
    int MaxWhatsAppMessages,
    string? GatewaySubscriptionId,
    SaasInvoiceDto? PendingInvoice,
    IReadOnlyList<SaasInvoiceDto> Invoices);

public sealed record ChangePlanRequest(
    SaasPlanTier TargetTier);

public sealed record AdminOverridePlanRequest(
    SaasPlanTier TargetTier,
    TenantSubscriptionStatus? NewStatus = null,
    string? Reason = null);

public sealed record SettleInvoiceRequest(
    bool SimulateFailure = false);

public sealed record SaasBillingWebhookPayload(
    string EventId,
    string EventType,
    Guid TenantId,
    string? GatewaySubscriptionId,
    string? GatewayInvoiceId,
    decimal? Amount,
    DateTimeOffset TimestampUtc,
    string? Signature = null);
