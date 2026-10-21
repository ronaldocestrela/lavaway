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
    int MaxTeamMembers,
    int MaxBoxes = 0,
    bool IsActive = true);

public sealed record UpdateSaasPlanRequest(
    string Name,
    string Description,
    decimal MonthlyPrice,
    int MaxWorkOrdersPerCycle,
    int MaxWhatsAppMessagesPerCycle,
    int MaxTeamMembers,
    int MaxBoxes,
    bool HasCustomerSubscriptions,
    bool HasLoyalty,
    bool HasCommissions,
    bool HasAiChatbot,
    bool IsActive);

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
    IReadOnlyList<SaasInvoiceDto> Invoices,
    bool HasCustomerSubscriptions = false,
    bool HasLoyalty = false,
    bool HasCommissions = false,
    bool HasAiChatbot = false,
    int MaxTeamMembers = 0,
    int MaxBoxes = 0,
    string? GatewayCustomerId = null,
    string? StripePortalUrl = null,
    string? PaymentMethodType = null);

public sealed record ChangePlanRequest(
    SaasPlanTier TargetTier);

public sealed record CreateStripeCheckoutSessionRequest(
    SaasPlanTier TargetTier,
    string? SuccessUrl = null,
    string? CancelUrl = null);

public sealed record StripeCheckoutSessionDto(
    string SessionId,
    string CheckoutUrl);

public sealed record StripeCustomerPortalSessionDto(
    string PortalUrl);

public sealed record AdminOverridePlanRequest(
    SaasPlanTier TargetTier,
    TenantSubscriptionStatus? NewStatus = null,
    string? Reason = null);

public sealed record SettleInvoiceRequest(
    bool SimulateFailure = false);

public sealed record SyncStripeCheckoutSessionRequest(
    string SessionId);

public sealed record SaasBillingWebhookPayload(
    string EventId,
    string EventType,
    Guid TenantId,
    string? GatewaySubscriptionId,
    string? GatewayInvoiceId,
    decimal? Amount,
    DateTimeOffset TimestampUtc,
    string? Signature = null,
    string? GatewayCustomerId = null,
    SaasPlanTier? PlanTier = null);
