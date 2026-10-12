using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Billing.Domain;

public sealed record GatewaySubscriptionResult(
    string GatewayCustomerId,
    string GatewaySubscriptionId,
    string InitialInvoiceId,
    string? PaymentUrl,
    string? PixQrCode,
    string? PixCopiaECola);

public sealed record GatewayInvoiceResult(
    string GatewayInvoiceId,
    decimal Amount,
    DateTimeOffset DueDateUtc,
    string? PaymentUrl,
    string? PixQrCode,
    string? PixCopiaECola);

public sealed record GatewayCheckoutSessionResult(
    string SessionId,
    string CheckoutUrl);

public sealed record GatewayPortalSessionResult(
    string PortalUrl);

public sealed record GatewayCheckoutSessionDetails(
    string SessionId,
    Guid TenantId,
    SaasPlanTier? PlanTier,
    string? GatewayCustomerId,
    string? GatewaySubscriptionId,
    string PaymentStatus,
    string Status);

public interface ISaasBillingGatewayProvider
{
    Task<Result<GatewaySubscriptionResult>> CreateSubscriptionAsync(
        Guid tenantId,
        string customerName,
        string customerEmail,
        SaasPlan plan,
        CancellationToken ct = default);

    Task<Result<GatewayCheckoutSessionResult>> CreateSubscriptionCheckoutSessionAsync(
        Guid tenantId,
        string customerName,
        string customerEmail,
        SaasPlan plan,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default);

    Task<Result<GatewayCheckoutSessionDetails>> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<Result<GatewayPortalSessionResult>> CreateCustomerPortalSessionAsync(
        string gatewayCustomerId,
        string returnUrl,
        CancellationToken ct = default);

    Task<Result<bool>> ChangeSubscriptionPlanAsync(
        string gatewaySubscriptionId,
        SaasPlan newPlan,
        CancellationToken ct = default);

    Task<Result<GatewayInvoiceResult>> GenerateInvoiceAsync(
        Guid tenantId,
        string gatewayCustomerId,
        decimal amount,
        DateTimeOffset dueDateUtc,
        CancellationToken ct = default);

    bool VerifyWebhookSignature(string payload, string signatureHeader, string secret);
}

